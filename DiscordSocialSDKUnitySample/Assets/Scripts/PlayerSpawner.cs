using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

#if DISCORD_SOCIAL_SDK_EXISTS
using Discord.Sdk;
#endif

/// PlayerSpawner handles spawning and despawning the player GameObject based on Discord lobby state.
/// The player is spawned when a lobby is created and destroyed when the lobby is left or deleted.
/// In multiplayer mode, it uses NetworkObject spawning to synchronize players across the network.
/// Only the host can spawn NetworkObjects - clients notify host via NetworkGameManager.
public class PlayerSpawner : MonoBehaviour
{
    [Header("Player Settings")]
    [SerializeField] private GameObject playerPrefab;
    
    [Header("Spawn Settings")]
    [SerializeField] private Vector3 spawnPosition = Vector3.zero;
    [Tooltip("If true, will spawn the player at this spawner's position instead of the configured spawn position")]
    [SerializeField] private bool useThisTransformPosition = false;
    
    [Header("Multiplayer Spawn Settings")]
    [Tooltip("Offset between spawn positions for multiple players")]
    [SerializeField] private float spawnOffset = 2f;

    private GameObject currentPlayerInstance;
    private bool isInLobby = false;
    private bool isLocalPlayerOnly = false; // Track if player is local-only (pre-lobby)
    
    // Track all spawned network players
    private Dictionary<ulong, GameObject> spawnedPlayers = new Dictionary<ulong, GameObject>();

#if DISCORD_SOCIAL_SDK_EXISTS
    void Start()
    {
        // Subscribe to Discord lobby events
        if (DiscordManager.Instance != null)
        {
            DiscordManager.Instance.OnDiscordLobbyCreated += OnLobbyCreated;
            DiscordManager.Instance.OnDiscordLobbyDeleted += OnLobbyDeleted;
        }
        else
        {
            Debug.LogError("PlayerSpawner: DiscordManager.Instance is null! Make sure DiscordManager exists in the scene.");
        }

        // Validate player prefab
        if (playerPrefab == null)
        {
            Debug.LogError("PlayerSpawner: Player prefab is not assigned! Please assign it in the Inspector.");
            return;
        }

        // Spawn local-only player immediately (before joining lobby)
        SpawnLocalOnlyPlayer();
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (DiscordManager.Instance != null)
        {
            DiscordManager.Instance.OnDiscordLobbyCreated -= OnLobbyCreated;
            DiscordManager.Instance.OnDiscordLobbyDeleted -= OnLobbyDeleted;
        }

        // Clean up player if it exists
        DespawnPlayer();
    }

    private void OnLobbyCreated(ulong lobbyId)
    {
        Debug.Log($"PlayerSpawner: Lobby created (ID: {lobbyId}). Transitioning to networked player...");
        isInLobby = true;
        
        // Destroy local-only player if it exists
        if (isLocalPlayerOnly && currentPlayerInstance != null)
        {
            Debug.Log("PlayerSpawner: Destroying local-only player before spawning networked version");
            Destroy(currentPlayerInstance);
            currentPlayerInstance = null;
            isLocalPlayerOnly = false;
        }
        
        // Wait for network to be ready before spawning
        if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsConnected)
        {
            SpawnNetworkedPlayer();
        }
        else
        {
            // Wait a frame for network to initialize
            StartCoroutine(WaitForNetworkAndSpawn());
        }
    }

    private System.Collections.IEnumerator WaitForNetworkAndSpawn()
    {
        // Wait up to 5 seconds for network to be ready
        float waitTime = 0f;
        while (waitTime < 5f)
        {
            if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsConnected)
            {
                SpawnNetworkedPlayer();
                yield break;
            }
            waitTime += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }
        
        Debug.LogError("PlayerSpawner: Network failed to connect in time!");
    }

    private void SpawnLocalOnlyPlayer()
    {
        // Spawn a simple local player (no networking) for solo play before joining lobby
        if (currentPlayerInstance != null)
        {
            Debug.LogWarning("PlayerSpawner: Player already exists.");
            return;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("PlayerSpawner: Cannot spawn player - no prefab assigned!");
            return;
        }

        Vector3 finalSpawnPosition = useThisTransformPosition ? transform.position : spawnPosition;
        
        // Instantiate without network components
        currentPlayerInstance = Instantiate(playerPrefab, finalSpawnPosition, Quaternion.identity);
        currentPlayerInstance.name = "Player (Local Only)";
        
        // Disable network components if they exist on the prefab
        NetworkObject netObj = currentPlayerInstance.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.enabled = false;
        }
        
        NetworkPlayerController netPlayerController = currentPlayerInstance.GetComponent<NetworkPlayerController>();
        if (netPlayerController != null)
        {
            netPlayerController.enabled = false;
        }
        
        PlayerNetworkData netData = currentPlayerInstance.GetComponent<PlayerNetworkData>();
        if (netData != null)
        {
            netData.enabled = false;
        }
        
        // Ensure PlayerController is enabled for local movement
        PlayerController playerController = currentPlayerInstance.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.SetCanMove(true);
        }
        
        isLocalPlayerOnly = true;
        Debug.Log($"PlayerSpawner: Local-only player spawned at {finalSpawnPosition} (pre-lobby)");
    }

    private void OnLobbyDeleted(ulong lobbyId)
    {
        Debug.Log($"PlayerSpawner: Lobby deleted (ID: {lobbyId}). Returning to local-only mode...");
        isInLobby = false;
        DespawnAllPlayers();
        
        // Respawn a local-only player after leaving lobby
        SpawnLocalOnlyPlayer();
    }

    private void SpawnNetworkedPlayer()
    {
        // Don't spawn if networked player already exists
        if (currentPlayerInstance != null && !isLocalPlayerOnly)
        {
            Debug.LogWarning("PlayerSpawner: Networked player already exists. Skipping spawn.");
            return;
        }

        // Don't spawn if no prefab is assigned
        if (playerPrefab == null)
        {
            Debug.LogError("PlayerSpawner: Cannot spawn player - no prefab assigned!");
            return;
        }

        if (NetworkGameManager.Instance == null || NetworkGameManager.Instance.GetNetworkManager() == null)
        {
            Debug.LogError("PlayerSpawner: NetworkGameManager or NetworkManager is null!");
            return;
        }

        var networkManager = NetworkGameManager.Instance.GetNetworkManager();
        
        // Check if we're the host or a client
        if (networkManager.IsHost || networkManager.IsServer)
        {
            // Host spawns their own player
            Debug.Log("PlayerSpawner: Host spawning own player...");
            SpawnPlayerForClient(networkManager.LocalClientId);
        }
        else if (networkManager.IsClient)
        {
            // Client requests host to spawn their player via ServerRpc
            Debug.Log("PlayerSpawner: Client requesting spawn from host...");
            NetworkGameManager.Instance.RequestPlayerSpawn(networkManager.LocalClientId);
        }
    }

    public void SpawnPlayerForClient(ulong clientId)
    {
        // Determine spawn position (with offset based on number of players)
        Vector3 finalSpawnPosition = useThisTransformPosition ? transform.position : spawnPosition;
        finalSpawnPosition += new Vector3(spawnedPlayers.Count * spawnOffset, 0, 0);

        // Instantiate the player
        GameObject playerInstance = Instantiate(playerPrefab, finalSpawnPosition, Quaternion.identity);
        
        // Get the NetworkObject component
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError("PlayerSpawner: Player prefab is missing NetworkObject component!");
            Destroy(playerInstance);
            return;
        }

        // Spawn the network object with ownership assigned to the client
        networkObject.SpawnAsPlayerObject(clientId);
        
        // Track this player
        ulong networkId = networkObject.NetworkObjectId;
        spawnedPlayers[networkId] = playerInstance;

        // If this is our local player, keep a reference
        var networkManager = NetworkGameManager.Instance.GetNetworkManager();
        if (clientId == networkManager.LocalClientId)
        {
            currentPlayerInstance = playerInstance;
        }

        Debug.Log($"PlayerSpawner: Player spawned for client {clientId} at {finalSpawnPosition} with NetworkObjectId {networkId}");
    }

    private void DespawnPlayer()
    {
        if (currentPlayerInstance != null)
        {
            NetworkObject networkObject = currentPlayerInstance.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                networkObject.Despawn();
            }
            
            Destroy(currentPlayerInstance);
            currentPlayerInstance = null;
            isLocalPlayerOnly = false;
            Debug.Log("PlayerSpawner: Player despawned.");
        }
    }

    private void DespawnAllPlayers()
    {
        // Despawn local player
        DespawnPlayer();
        
        // Despawn all tracked network players
        foreach (var kvp in spawnedPlayers)
        {
            if (kvp.Value != null)
            {
                NetworkObject networkObject = kvp.Value.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn();
                }
                Destroy(kvp.Value);
            }
        }
        
        spawnedPlayers.Clear();
        isLocalPlayerOnly = false;
        Debug.Log("PlayerSpawner: All players despawned.");
    }

    /// Get the current player instance (if spawned)
    public GameObject GetPlayerInstance()
    {
        return currentPlayerInstance;
    }

    /// Check if player is currently spawned
    public bool IsPlayerSpawned()
    {
        return currentPlayerInstance != null;
    }

    /// Check if currently in a lobby
    public bool IsInLobby()
    {
        return isInLobby;
    }

    /// Manually set the spawn position (can be called by other scripts)
    public void SetSpawnPosition(Vector3 position)
    {
        spawnPosition = position;
    }
#else
    void Start()
    {
        Debug.LogWarning("PlayerSpawner: Discord Social SDK is not enabled. Player spawning is disabled.");
    }
#endif
}

