using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if DISCORD_SOCIAL_SDK_EXISTS
using Discord.Sdk;
#endif

/// <summary>
/// One-stop manager for the proximity-audio demo. Walks top-to-bottom through:
///   1. Creating / joining / leaving a Discord lobby
///   2. Spawning local + remote player prefabs from lobby membership
///   3. Starting the Discord voice call and routing per-user audio into a
///      spatial AudioSource on each remote player (this is what makes the
///      audio proximity-based instead of a flat 2-channel mix).
///
/// External companions:
///   - Lobby.cs is a thin shim that forwards into this script for existing
///     scene wiring (Invite, LobbyInviteModal).
///   - PositionSyncClient.cs owns the WebSocket position-sync orchestration
///     by subscribing to OnLobbyJoined / OnLobbyLeft and reading
///     LocalPlayerTransform / GetRemotePlayer from here.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button leaveLobbyButton;

    [Header("Lobby")]
    [SerializeField] private int maxLobbySize = 4;

    [Header("Prefabs")]
    [Tooltip("Prefab with PlayerMovement, Rigidbody, and CapsuleCollider for the local player.")]
    [SerializeField] private GameObject localPlayerPrefab;
    [Tooltip("Prefab with RemotePlayer + AudioSource + VoiceAudioSource for other lobby members.")]
    [SerializeField] private GameObject remotePlayerPrefab;

    [Header("Scene")]
    [Tooltip("Where players spawn. If unset, uses world origin.")]
    [SerializeField] private Transform spawnPoint;

    public delegate void LobbyJoinedHandler(ulong lobbyId, string secret);
    public event LobbyJoinedHandler OnLobbyJoined;
    public event System.Action OnLobbyLeft;

    private ulong myUserId;
    private ulong currentLobbyId;
    private string lobbySecret = string.Empty;

    private GameObject localPlayerObj;
    private readonly Dictionary<ulong, RemotePlayer> remotePlayers = new();

    public bool IsInLobby => currentLobbyId != 0;
    public ulong CurrentLobbyId => currentLobbyId;
    public string LobbySecret => lobbySecret;
    public ulong MyUserId => myUserId;
    public Transform LocalPlayerTransform => localPlayerObj != null ? localPlayerObj.transform : null;
    public RemotePlayer GetRemotePlayer(ulong userId) =>
        remotePlayers.TryGetValue(userId, out var p) ? p : null;

    void Awake()
    {
        Instance = this;
    }

#if DISCORD_SOCIAL_SDK_EXISTS
    private Client client;
    private RichPresence richPresence;
    private Call activeCall;
    private readonly ConcurrentDictionary<ulong, VoiceAudioSource> voiceSources = new();

    // ── Setup ─────────────────────────────────────────────────────────────────

    void Start()
    {
        client = DiscordManager.Instance.GetClient();
        richPresence = FindFirstObjectByType<RichPresence>();

        DiscordManager.Instance.OnDiscordStatusChanged += OnStatusChanged;
        DiscordManager.Instance.OnDiscordLobbyMemberAdded += OnLobbyMemberAdded;
        DiscordManager.Instance.OnDiscordLobbyMemberRemoved += OnLobbyMemberRemoved;
        DiscordManager.Instance.OnDiscordLobbyDeleted += OnDiscordLobbyDeleted;
        DiscordManager.Instance.OnDiscordSetActivityJoinCallback += OnSetActivityJoinCallback;

        client.SetDeviceChangeCallback(OnAudioDevicesChanged);

        createLobbyButton.onClick.AddListener(CreateLobby);
        leaveLobbyButton.onClick.AddListener(LeaveLobby);
        createLobbyButton.gameObject.SetActive(false);
        leaveLobbyButton.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (DiscordManager.Instance != null)
        {
            DiscordManager.Instance.OnDiscordStatusChanged -= OnStatusChanged;
            DiscordManager.Instance.OnDiscordLobbyMemberAdded -= OnLobbyMemberAdded;
            DiscordManager.Instance.OnDiscordLobbyMemberRemoved -= OnLobbyMemberRemoved;
            DiscordManager.Instance.OnDiscordLobbyDeleted -= OnDiscordLobbyDeleted;
            DiscordManager.Instance.OnDiscordSetActivityJoinCallback -= OnSetActivityJoinCallback;
        }

        if (client != null && currentLobbyId != 0)
            client.LeaveLobby(currentLobbyId, (ClientResult _) => { });
    }

    private void OnStatusChanged(Client.Status status, Client.Error error, int errorCode)
    {
        if (status != Client.Status.Ready) return;

        var user = client.GetCurrentUserV2();
        if (user != null) myUserId = user.Id();

        createLobbyButton.gameObject.SetActive(true);
    }

    // ── 1. Creating / joining / leaving a lobby ──────────────────────────────

    private void CreateLobby()
    {
        lobbySecret = System.Guid.NewGuid().ToString();
        DiscordManager.Instance.OnLog($"Creating lobby {lobbySecret}", LoggingSeverity.Warning);
        createLobbyButton.gameObject.SetActive(false);
        client.CreateOrJoinLobby(lobbySecret, OnCreateOrJoinLobby);
    }

    public void JoinLobby(string secret)
    {
        lobbySecret = secret;
        DiscordManager.Instance.OnLog($"Joining lobby {secret}", LoggingSeverity.Warning);
        createLobbyButton.gameObject.SetActive(false);
        client.CreateOrJoinLobby(lobbySecret, OnCreateOrJoinLobby);
    }

    private void OnSetActivityJoinCallback(string secret)
    {
        DiscordManager.Instance.OnLog($"Activity-join callback received secret {secret}", LoggingSeverity.Warning);
        JoinLobby(secret);
    }

    private void OnCreateOrJoinLobby(ClientResult result, ulong lobbyId)
    {
        if (!result.Successful())
        {
            Debug.LogError($"Failed to create or join lobby: {result}");
            createLobbyButton.gameObject.SetActive(true);
            return;
        }

        currentLobbyId = lobbyId;
        leaveLobbyButton.gameObject.SetActive(true);

        if (richPresence != null)
        {
            richPresence.UpdateRichPresenceLobby(
                ActivityTypes.Playing, "In Lobby", "Waiting for players",
                lobbySecret, lobbyId.ToString(), maxLobbySize);
        }

        SpawnLocalPlayer();

        // Anyone already in the lobby when we joined needs a remote prefab too.
        var lobbyHandle = client.GetLobbyHandle(lobbyId);
        if (lobbyHandle != null)
        {
            foreach (var memberId in lobbyHandle.LobbyMemberIds())
                if (memberId != myUserId) SpawnRemotePlayer(memberId);
        }

        StartAudioCall();

        OnLobbyJoined?.Invoke(lobbyId, lobbySecret);
    }

    private void LeaveLobby()
    {
        if (currentLobbyId == 0) return;
        leaveLobbyButton.gameObject.SetActive(false);
        client.LeaveLobby(currentLobbyId, OnLeaveLobby);
    }

    private void OnLeaveLobby(ClientResult result)
    {
        if (!result.Successful())
        {
            Debug.LogError($"Failed to leave lobby: {result}");
            leaveLobbyButton.gameObject.SetActive(true);
            return;
        }

        TearDownLobby();
        createLobbyButton.gameObject.SetActive(true);
        if (richPresence != null) richPresence.SetDefaultRichPresence();
    }

    private void OnDiscordLobbyDeleted(ulong lobbyId)
    {
        if (lobbyId != currentLobbyId) return;
        TearDownLobby();
        createLobbyButton.gameObject.SetActive(true);
    }

    private void TearDownLobby()
    {
        EndAudioCall();
        DespawnLocalPlayer();
        DespawnAllRemotePlayers();
        currentLobbyId = 0;
        lobbySecret = string.Empty;
        OnLobbyLeft?.Invoke();
    }

    // ── 2. Spawning players from lobby membership ────────────────────────────

    private void OnLobbyMemberAdded(ulong lobbyId, ulong userId)
    {
        if (lobbyId != currentLobbyId) return;
        if (userId == myUserId) return;
        SpawnRemotePlayer(userId);
    }

    private void OnLobbyMemberRemoved(ulong lobbyId, ulong userId)
    {
        if (lobbyId != currentLobbyId) return;
        if (!remotePlayers.TryGetValue(userId, out var remote)) return;
        Destroy(remote.gameObject);
        remotePlayers.Remove(userId);
        voiceSources.TryRemove(userId, out _);
    }

    private void SpawnLocalPlayer()
    {
        if (localPlayerObj != null) return;
        localPlayerObj = Instantiate(localPlayerPrefab, SpawnPosition(), SpawnRotation());
    }

    private void SpawnRemotePlayer(ulong userId)
    {
        if (remotePlayers.ContainsKey(userId)) return;
        var go = Instantiate(remotePlayerPrefab, SpawnPosition(), SpawnRotation());
        remotePlayers[userId] = go.GetComponent<RemotePlayer>();

        var voice = go.GetComponent<VoiceAudioSource>();
        if (voice != null)
            voiceSources[userId] = voice;
        else
            Debug.LogWarning($"[GameManager] RemotePlayer prefab missing VoiceAudioSource for userId {userId}");
    }

    private void DespawnLocalPlayer()
    {
        if (localPlayerObj == null) return;
        Destroy(localPlayerObj);
        localPlayerObj = null;
    }

    private void DespawnAllRemotePlayers()
    {
        foreach (var remote in remotePlayers.Values)
            if (remote != null) Destroy(remote.gameObject);
        remotePlayers.Clear();
        voiceSources.Clear();
    }

    private Vector3 SpawnPosition() => spawnPoint != null ? spawnPoint.position : Vector3.zero;
    private Quaternion SpawnRotation() => spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

    // ── 3. Audio call + per-player spatial routing ───────────────────────────

    private void StartAudioCall()
    {
        // The first callback is the user-audio receiver — Discord hands us raw int16 PCM
        // per remote user on its audio thread. The second is for our own captured mic
        // audio, which we don't need to inspect for this demo.
        activeCall = client.StartCallWithAudioCallbacks(
            currentLobbyId,
            OnVoiceAudioReceived,
            (data, samplesPerChannel, sampleRate, channels) => { });

        if (activeCall == null) return;

        // VAD off (threshold -100 dB ≈ always-on). Unity's spatial AudioSource
        // will attenuate quiet/distant players naturally — we don't want Discord's
        // voice-activity gate cutting samples before they reach the spatializer.
        activeCall.SetVADThreshold(false, -100f);

        // Free side-benefit of having the call running: trigger mouth animation
        // on each RemotePlayer while their audio energy crosses Discord's threshold.
        activeCall.SetSpeakingStatusChangedCallback((ulong userId, bool isPlayingSound) =>
        {
            if (remotePlayers.TryGetValue(userId, out var remote))
                remote.SetSpeaking(isPlayingSound);
        });
    }

    private void EndAudioCall()
    {
        if (activeCall == null) return;
        client.EndCall(currentLobbyId, () => { });
        activeCall = null;
    }

    // Called by Discord on its audio thread with one user's PCM frame.
    private void OnVoiceAudioReceived(ulong userId, System.IntPtr data, ulong samplesPerChannel,
                                      int sampleRate, ulong channels, ref bool outShouldMute)
    {
        // THE proximity-audio line: mute Discord's default 2-channel playback so the
        // only path this audio takes is through the per-player spatial AudioSource
        // below. Without this you'd hear two copies — flat + spatial — and lose proximity.
        outShouldMute = true;

        if (voiceSources.TryGetValue(userId, out var voiceSource))
            voiceSource.FeedSamples(data, samplesPerChannel, channels);
    }

    private void OnAudioDevicesChanged(AudioDevice[] inputDevices, AudioDevice[] outputDevices)
    {
        foreach (var device in inputDevices)
            DiscordManager.Instance.OnLog($"[Audio Device] Input: \"{device.Name()}\" id={device.Id()} default={device.IsDefault()}", LoggingSeverity.Warning);
        foreach (var device in outputDevices)
            DiscordManager.Instance.OnLog($"[Audio Device] Output: \"{device.Name()}\" id={device.Id()} default={device.IsDefault()}", LoggingSeverity.Warning);
    }
#endif
}
