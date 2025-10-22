using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

#if DISCORD_SOCIAL_SDK_EXISTS
using Discord.Sdk;
#endif

/// <summary>
/// NetworkGameManager bridges Discord lobby system with Unity Netcode for GameObjects.
/// It handles starting/stopping the network based on Discord lobby events and manages
/// peer-to-peer connections where the first player becomes the host.
/// </summary>
public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [Header("Network Settings")]
    [SerializeField] private NetworkManager networkManager;

    private bool isHost = false;
    private bool isConnected = false;
    private ulong currentLobbyId = 0;

    // Track players by Discord user ID
    private Dictionary<ulong, ulong> discordIdToNetworkId = new Dictionary<ulong, ulong>();
    
    // Track pending spawn requests
    private HashSet<ulong> pendingSpawns = new HashSet<ulong>();

#if DISCORD_SOCIAL_SDK_EXISTS
    private Client discordClient;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Validate NetworkManager reference
        if (networkManager == null)
        {
            networkManager = GetComponent<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("NetworkGameManager: NetworkManager component not found!");
            }
        }
    }

    void Start()
    {
        // Subscribe to Discord events
        if (DiscordManager.Instance != null)
        {
            DiscordManager.Instance.OnDiscordLobbyCreated += OnLobbyCreated;
            DiscordManager.Instance.OnDiscordLobbyDeleted += OnLobbyDeleted;
            DiscordManager.Instance.OnDiscordLobbyMemberAdded += OnLobbyMemberAdded;
            DiscordManager.Instance.OnDiscordLobbyMemberRemoved += OnLobbyMemberRemoved;
            
            discordClient = DiscordManager.Instance.GetClient();
        }
        else
        {
            Debug.LogError("NetworkGameManager: DiscordManager.Instance is null!");
        }

        // Subscribe to network events
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            networkManager.OnServerStarted += OnServerStarted;
        }
    }

    private void OnServerStarted()
    {
        // Spawn this NetworkGameManager on the network when server starts
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned)
        {
            netObj.Spawn();
            Debug.Log("NetworkGameManager: Spawned on network");
        }
    }


    void OnDestroy()
    {
        // Unsubscribe from Discord events
        if (DiscordManager.Instance != null)
        {
            DiscordManager.Instance.OnDiscordLobbyCreated -= OnLobbyCreated;
            DiscordManager.Instance.OnDiscordLobbyDeleted -= OnLobbyDeleted;
            DiscordManager.Instance.OnDiscordLobbyMemberAdded -= OnLobbyMemberAdded;
            DiscordManager.Instance.OnDiscordLobbyMemberRemoved -= OnLobbyMemberRemoved;
        }

        // Unsubscribe from network events
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        // Shutdown network if active
        ShutdownNetwork();
    }

    private void OnLobbyCreated(ulong lobbyId)
    {
        Debug.Log($"NetworkGameManager: Lobby created/joined (ID: {lobbyId}). Determining role...");
        currentLobbyId = lobbyId;
        
        // Determine if we're the host (first in lobby) or client (joining existing)
        if (discordClient != null)
        {
            var lobby = discordClient.GetLobbyHandle(lobbyId);
            int memberCount = lobby.LobbyMemberIds().Length;
            
            Debug.Log($"NetworkGameManager: Lobby has {memberCount} member(s)");
            
            // If we're the only member, we're creating the lobby and should be host
            // If there are already other members, we're joining and should be client
            if (memberCount == 1)
            {
                Debug.Log("NetworkGameManager: First member - becoming HOST");
                StartHost();
            }
            else
            {
                Debug.Log("NetworkGameManager: Joining existing lobby - becoming CLIENT");
                StartClient();
            }
        }
        else
        {
            Debug.LogError("NetworkGameManager: Discord client is null!");
        }
    }

    private void OnLobbyDeleted(ulong lobbyId)
    {
        Debug.Log($"NetworkGameManager: Lobby deleted (ID: {lobbyId}). Shutting down network...");
        currentLobbyId = 0;
        ShutdownNetwork();
    }

    private void OnLobbyMemberAdded(ulong lobbyId, ulong userId)
    {
        Debug.Log($"NetworkGameManager: Member added to lobby {lobbyId} - Discord User ID: {userId}");
        
        // If we're the host and this isn't us joining, a new player needs to connect
        if (isHost && discordClient != null)
        {
            ulong currentUserId = discordClient.GetCurrentUser().Id();
            if (userId != currentUserId)
            {
                Debug.Log($"NetworkGameManager: New player joining. Waiting for network connection...");
            }
        }
    }

    private void OnLobbyMemberRemoved(ulong lobbyId, ulong userId)
    {
        Debug.Log($"NetworkGameManager: Member removed from lobby {lobbyId} - Discord User ID: {userId}");
        
        // Remove from our tracking dictionary
        if (discordIdToNetworkId.ContainsKey(userId))
        {
            discordIdToNetworkId.Remove(userId);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"NetworkGameManager: Client connected - Network ID: {clientId}");
        
        if (networkManager.IsHost && clientId != networkManager.LocalClientId)
        {
            Debug.Log($"NetworkGameManager: Remote client {clientId} connected to host.");
        }
        else if (clientId == networkManager.LocalClientId)
        {
            Debug.Log($"NetworkGameManager: Local client connected successfully.");
            isConnected = true;
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"NetworkGameManager: Client disconnected - Network ID: {clientId}");
        
        // Clean up player for this client
        // This will be handled by PlayerSpawner when it detects the disconnect
    }

    private void StartHost()
    {
        if (networkManager == null)
        {
            Debug.LogError("NetworkGameManager: Cannot start host - NetworkManager is null!");
            return;
        }

        if (isConnected)
        {
            Debug.LogWarning("NetworkGameManager: Already connected to network!");
            return;
        }

        bool success = networkManager.StartHost();
        if (success)
        {
            isHost = true;
            isConnected = true;
            Debug.Log("NetworkGameManager: Successfully started as HOST.");
        }
        else
        {
            Debug.LogError("NetworkGameManager: Failed to start as host!");
        }
    }

    public void StartClient()
    {
        if (networkManager == null)
        {
            Debug.LogError("NetworkGameManager: Cannot start client - NetworkManager is null!");
            return;
        }

        if (isConnected)
        {
            Debug.LogWarning("NetworkGameManager: Already connected to network!");
            return;
        }

        bool success = networkManager.StartClient();
        if (success)
        {
            isHost = false;
            Debug.Log("NetworkGameManager: Starting as CLIENT...");
        }
        else
        {
            Debug.LogError("NetworkGameManager: Failed to start as client!");
        }
    }

    private void ShutdownNetwork()
    {
        if (networkManager == null || !isConnected)
            return;

        Debug.Log("NetworkGameManager: Shutting down network...");
        
        networkManager.Shutdown();
        isHost = false;
        isConnected = false;
        discordIdToNetworkId.Clear();
        
        Debug.Log("NetworkGameManager: Network shutdown complete.");
    }

    // Public getters
    public bool IsHost => isHost;
    public bool IsConnected => isConnected;
    public ulong CurrentLobbyId => currentLobbyId;
    public NetworkManager GetNetworkManager() => networkManager;

    // Track Discord ID to Network ID mapping
    public void MapDiscordToNetwork(ulong discordId, ulong networkId)
    {
        discordIdToNetworkId[discordId] = networkId;
        Debug.Log($"NetworkGameManager: Mapped Discord ID {discordId} to Network ID {networkId}");
    }

    public bool TryGetNetworkId(ulong discordId, out ulong networkId)
    {
        return discordIdToNetworkId.TryGetValue(discordId, out networkId);
    }

    // Client calls this to request the host spawn a player for them
    public void RequestPlayerSpawn(ulong clientId)
    {
        if (networkManager.IsClient && !networkManager.IsHost)
        {
            // Only clients need to request spawn via ServerRpc
            RequestPlayerSpawnServerRpc(clientId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayerSpawnServerRpc(ulong clientId)
    {
        Debug.Log($"NetworkGameManager: Received spawn request for client {clientId}");
        
        // Avoid duplicate spawns
        if (pendingSpawns.Contains(clientId))
        {
            Debug.LogWarning($"NetworkGameManager: Client {clientId} spawn already pending");
            return;
        }
        
        pendingSpawns.Add(clientId);
        
        // Find PlayerSpawner and spawn for this client
        PlayerSpawner spawner = FindFirstObjectByType<PlayerSpawner>();
        if (spawner != null)
        {
            spawner.SpawnPlayerForClient(clientId);
            pendingSpawns.Remove(clientId);
        }
        else
        {
            Debug.LogError("NetworkGameManager: PlayerSpawner not found in scene!");
        }
    }


#else
    void Start()
    {
        Debug.LogWarning("NetworkGameManager: Discord Social SDK is not enabled. Networking is disabled.");
    }
#endif
}

