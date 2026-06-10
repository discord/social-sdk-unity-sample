using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Discord.Sdk;

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

    private Client client;
    private RichPresence richPresence;
    private Call activeCall;
    private readonly ConcurrentDictionary<ulong, VoiceAudioSource> voiceSources = new();

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

    private void CreateLobby()
    {
        lobbySecret = System.Guid.NewGuid().ToString();
        createLobbyButton.gameObject.SetActive(false);
        client.CreateOrJoinLobby(lobbySecret, OnCreateOrJoinLobby);
    }

    public void JoinLobby(string secret)
    {
        lobbySecret = secret;
        createLobbyButton.gameObject.SetActive(false);
        client.CreateOrJoinLobby(lobbySecret, OnCreateOrJoinLobby);
    }

    private void OnCreateOrJoinLobby(ClientResult result, ulong lobbyId)
    {
        if(!result.Successful())
        {
            Debug.LogError("Lobby creation or join was unsuccessful");
            createLobbyButton.gameObject.SetActive(true);
            return;
        }

        currentLobbyId = lobbyId;
        leaveLobbyButton.gameObject.SetActive(true);

        richPresence.UpdateRichPresenceWithLobby(ActivityTypes.Playing, "In Lobby", "Waiting for friends", lobbySecret, lobbyId.ToString(), maxLobbySize);

        SpawnLocalPlayer();
        SpawnExistingRemotePlayers(lobbyId);

        StartAudioCall();

        OnLobbyJoined?.Invoke(lobbyId, lobbySecret);
    }

    private void OnSetActivityJoinCallback(string secret)
    {
        DiscordManager.Instance.OnLog($"Activity-join callback received secret {secret}", LoggingSeverity.Warning);
        JoinLobby(secret);
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

    private void SpawnExistingRemotePlayers(ulong lobbyId)
    {
        // Anyone already in the lobby when we joined needs a remote prefab too.
        var lobbyHandle = client.GetLobbyHandle(lobbyId);
        if (lobbyHandle != null)
        {
            foreach (var memberId in lobbyHandle.LobbyMemberIds())
                if (memberId != myUserId) SpawnRemotePlayer(memberId);
        }
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

    private void StartAudioCall()
    {
        activeCall = client.StartCallWithAudioCallbacks(currentLobbyId, OnVoiceAudioReceived, (data, samplesPerChannel, sampleRate, channels) => { });

        if (activeCall == null)
        {
            Debug.LogWarning("Unable to create call");
            return;
        }

        activeCall.SetVADThreshold(false, -100f);
    }

    private void EndAudioCall()
    {
        if(activeCall == null)
        {
            return;
        }
        client.EndCall(currentLobbyId, () => { });
        activeCall = null;
    }

    // Called by Discord on its audio thread with one user's PCM frame.
    private void OnVoiceAudioReceived(ulong userId, System.IntPtr data, ulong samplesPerChannel,
                                      int sampleRate, ulong channels, ref bool outShouldMute)
    {
        outShouldMute = true;

        if(voiceSources.TryGetValue(userId, out var voiceSource))
        {
            voiceSource.FeedSamples(data, samplesPerChannel, channels);
        }
    }

    private void OnAudioDevicesChanged(AudioDevice[] inputDevices, AudioDevice[] outputDevices)
    {
        foreach (var device in inputDevices)
            DiscordManager.Instance.OnLog($"[Audio Device] Input: \"{device.Name()}\" id={device.Id()} default={device.IsDefault()}", LoggingSeverity.Warning);
        foreach (var device in outputDevices)
            DiscordManager.Instance.OnLog($"[Audio Device] Output: \"{device.Name()}\" id={device.Id()} default={device.IsDefault()}", LoggingSeverity.Warning);
    }
}
