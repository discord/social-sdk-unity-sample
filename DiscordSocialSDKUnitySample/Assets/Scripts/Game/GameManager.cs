using UnityEngine;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;

#if DISCORD_SOCIAL_SDK_EXISTS
using Discord.Sdk;
#endif

/// <summary>
/// Manages spawning/despawning of local and remote players based on Discord lobby
/// membership, and drives position synchronization via PositionSyncClient.
///
/// Spawn/despawn is authoritative from Discord lobby events.
/// Position updates are sent to and received from the WebSocket position-sync server.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Prefab with PlayerMovement, Rigidbody, and CapsuleCollider for the local player.")]
    [SerializeField] private GameObject localPlayerPrefab;
    [Tooltip("Prefab with RemotePlayer script for other lobby members.")]
    [SerializeField] private GameObject remotePlayerPrefab;

    [Header("Scene")]
    [Tooltip("Where players spawn. If unset, uses world origin.")]
    [SerializeField] private Transform spawnPoint;

    [Header("Position Sync")]
    [Tooltip("PositionSyncClient component that manages the WebSocket connection.")]
    [SerializeField] private PositionSyncClient positionSyncClient;
    [Tooltip("How often (seconds) the local player's position is sent to the server.")]
    [SerializeField] private float positionSendInterval = 0.1f;

#if DISCORD_SOCIAL_SDK_EXISTS
    private Client client;
    private Lobby lobby;
    private ulong myUserId;
    private ulong currentLobbyId;
    private Call activeCall;

    private GameObject localPlayerObj;
    private readonly Dictionary<ulong, RemotePlayer> remotePlayers = new();
    private readonly ConcurrentDictionary<ulong, VoiceAudioSource> voiceSources = new();

    void Start()
    {
        client = DiscordManager.Instance.GetClient();
        lobby = FindFirstObjectByType<Lobby>();

        client.SetDeviceChangeCallback(OnAudioDevicesChanged);

        DiscordManager.Instance.OnDiscordStatusChanged += OnStatusChanged;
        DiscordManager.Instance.OnDiscordLobbyMemberAdded += OnLobbyMemberAdded;
        DiscordManager.Instance.OnDiscordLobbyMemberRemoved += OnLobbyMemberRemoved;
        DiscordManager.Instance.OnDiscordLobbyDeleted += OnLobbyDeleted;

        if (lobby != null)
        {
            lobby.OnLobbyJoined += OnLobbyJoined;
            lobby.OnLobbyLeft += OnLobbyLeft;
        }

        if (positionSyncClient != null)
        {
            positionSyncClient.OnWelcome += OnSyncWelcome;
            positionSyncClient.OnPositionReceived += OnSyncPositionReceived;
        }
    }

    void OnDestroy()
    {
        if (DiscordManager.Instance != null)
        {
            DiscordManager.Instance.OnDiscordStatusChanged -= OnStatusChanged;
            DiscordManager.Instance.OnDiscordLobbyMemberAdded -= OnLobbyMemberAdded;
            DiscordManager.Instance.OnDiscordLobbyMemberRemoved -= OnLobbyMemberRemoved;
            DiscordManager.Instance.OnDiscordLobbyDeleted -= OnLobbyDeleted;
        }

        if (lobby != null)
        {
            lobby.OnLobbyJoined -= OnLobbyJoined;
            lobby.OnLobbyLeft -= OnLobbyLeft;
        }

        if (positionSyncClient != null)
        {
            positionSyncClient.OnWelcome -= OnSyncWelcome;
            positionSyncClient.OnPositionReceived -= OnSyncPositionReceived;
        }
    }

    // ── Discord status ────────────────────────────────────────────────────────

    private void OnStatusChanged(Client.Status status, Client.Error error, int errorCode)
    {
        if (status == Client.Status.Ready)
        {
            var user = client.GetCurrentUserV2();
            if (user != null)
                myUserId = user.Id();
        }
    }

    // ── Lobby lifecycle ───────────────────────────────────────────────────────

    private void OnLobbyJoined(ulong lobbyId, string secret)
    {
        currentLobbyId = lobbyId;

        SpawnLocalPlayer();

        // Spawn remote players already in the lobby
        var lobbyHandle = client.GetLobbyHandle(lobbyId);
        if (lobbyHandle != null)
        {
            foreach (var memberId in lobbyHandle.LobbyMemberIds())
            {
                if (memberId != myUserId)
                    SpawnRemotePlayer(memberId);
            }
        }

        positionSyncClient?.Connect(secret, myUserId);

        activeCall = client.StartCallWithAudioCallbacks(currentLobbyId, OnVoiceAudioReceived,
            (data, samplesPerChannel, sampleRate, channels) => { });

        if (activeCall != null)
        {
            activeCall.SetVADThreshold(false, -100f);
            activeCall.SetSpeakingStatusChangedCallback((ulong userId, bool isPlayingSound) =>
            {
                if (remotePlayers.TryGetValue(userId, out var remote))
                    remote.SetSpeaking(isPlayingSound);
            });
        }

        StartCoroutine(SendPositionLoop());
    }

    private void OnLobbyLeft()
    {
        StopAllCoroutines();
        positionSyncClient?.Disconnect();
        client.EndCall(currentLobbyId, () => { });
        activeCall = null;
        DespawnLocalPlayer();
        DespawnAllRemotePlayers();
        currentLobbyId = 0;
    }

    private void OnLobbyDeleted(ulong lobbyId)
    {
        StopAllCoroutines();
        positionSyncClient?.Disconnect();
        client.EndCall(currentLobbyId, () => { });
        activeCall = null;
        DespawnLocalPlayer();
        DespawnAllRemotePlayers();
        currentLobbyId = 0;
    }

    // ── Lobby membership ─────────────────────────────────────────────────────

    private void OnLobbyMemberAdded(ulong lobbyId, ulong userId)
    {
        if (userId == myUserId) return;
        SpawnRemotePlayer(userId);
    }

    private void OnLobbyMemberRemoved(ulong lobbyId, ulong userId)
    {
        if (!remotePlayers.TryGetValue(userId, out var remote)) return;
        Destroy(remote.gameObject);
        remotePlayers.Remove(userId);
        voiceSources.TryRemove(userId, out _);
    }

    // ── Position sync events ──────────────────────────────────────────────────

    private void OnSyncWelcome(PositionSyncClient.PlayerState[] players)
    {
        foreach (var p in players)
        {
            if (!ulong.TryParse(p.userId, out var uid)) continue;
            if (!remotePlayers.TryGetValue(uid, out var remote)) continue;
            remote.SetTarget(new Vector3(p.x, p.y, p.z), p.yaw);
        }
    }

    private void OnSyncPositionReceived(ulong userId, Vector3 pos, float yaw)
    {
        if (!remotePlayers.TryGetValue(userId, out var remote)) return;
        remote.SetTarget(pos, yaw);
    }

    // ── Voice audio ───────────────────────────────────────────────────────────

    private void OnVoiceAudioReceived(ulong userId, System.IntPtr data, ulong samplesPerChannel,
                                      int sampleRate, ulong channels, ref bool outShouldMute)
    {
        // Intercept Discord's default output so audio plays only through the
        // per-player spatial AudioSource on each remote player prefab.
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

    // ── Spawning ─────────────────────────────────────────────────────────────

    private void SpawnLocalPlayer()
    {
        if (localPlayerObj != null) return;
        localPlayerObj = Instantiate(localPlayerPrefab, SpawnPosition(), SpawnRotation());
    }

    private void DespawnLocalPlayer()
    {
        if (localPlayerObj == null) return;
        Destroy(localPlayerObj);
        localPlayerObj = null;
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
            Debug.LogWarning($"[GameManager] RemotePlayer prefab is missing VoiceAudioSource for userId {userId}");
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

    // ── Position sending ──────────────────────────────────────────────────────

    private IEnumerator SendPositionLoop()
    {
        var wait = new WaitForSeconds(positionSendInterval);
        while (localPlayerObj != null && currentLobbyId != 0)
        {
            if (positionSyncClient != null && positionSyncClient.IsConnected)
                positionSyncClient.SendPosition(
                    localPlayerObj.transform.position,
                    localPlayerObj.transform.eulerAngles.y);
            yield return wait;
        }
    }
#endif
}
