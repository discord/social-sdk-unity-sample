using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// PlayerNameManager is a singleton that manages all player name displays.
/// It tracks networked players and updates their name UI positions each frame.
/// Attach this to a screen-space Canvas in your scene.
/// </summary>
public class PlayerNameManager : MonoBehaviour
{
    public static PlayerNameManager Instance { get; private set; }

    [Header("Name Display Settings")]
    [SerializeField] private GameObject playerNameUIPrefab;
    [Tooltip("World space offset above player (Y axis)")]
    [SerializeField] private float yOffset = 0.7f;
    [Tooltip("How often to check for new players (in seconds)")]
    [SerializeField] private float playerCheckInterval = 0.5f;

    private Camera mainCamera;
    private Canvas canvas;
    private Dictionary<ulong, PlayerNameDisplay> playerNameDisplays = new Dictionary<ulong, PlayerNameDisplay>();
    private float nextPlayerCheckTime = 0f;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Get or validate Canvas component
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("PlayerNameManager: No Canvas component found! This script must be attached to a Canvas.");
            enabled = false;
            return;
        }

        // Ensure Canvas is set to Screen Space - Overlay
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            Debug.LogWarning("PlayerNameManager: Canvas is not set to Screen Space - Overlay. Names may not display correctly.");
        }

        // Validate prefab
        if (playerNameUIPrefab == null)
        {
            Debug.LogError("PlayerNameManager: PlayerNameUI prefab is not assigned!");
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        mainCamera = Camera.main;
        
        if (mainCamera == null)
        {
            Debug.LogError("PlayerNameManager: No main camera found!");
        }
    }

    private void LateUpdate()
    {
        if (mainCamera == null) return;

        // Periodically check for new players
        if (Time.time >= nextPlayerCheckTime)
        {
            CheckForNewPlayers();
            nextPlayerCheckTime = Time.time + playerCheckInterval;
        }

        // Update all name positions
        UpdateAllNamePositions();

        // Clean up displays for destroyed players
        CleanupDestroyedPlayers();
    }

    /// <summary>
    /// Check for newly spawned networked players and create name displays for them
    /// </summary>
    private void CheckForNewPlayers()
    {
        // Only check if network is active
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            return;
        }

        // Find all NetworkObjects with PlayerNetworkData
        PlayerNetworkData[] allPlayers = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        
        foreach (PlayerNetworkData playerData in allPlayers)
        {
            NetworkObject netObj = playerData.GetComponent<NetworkObject>();
            
            // Only create display for spawned networked players
            if (netObj != null && netObj.IsSpawned)
            {
                ulong networkId = netObj.NetworkObjectId;
                
                // Create display if it doesn't exist
                if (!playerNameDisplays.ContainsKey(networkId))
                {
                    CreateNameDisplayForPlayer(playerData.transform, networkId);
                }
            }
        }
    }

    /// <summary>
    /// Create a name display for a specific player
    /// </summary>
    private void CreateNameDisplayForPlayer(Transform player, ulong networkId)
    {
        if (player == null || playerNameUIPrefab == null) return;

        // Instantiate the name UI as a child of this canvas
        GameObject nameUIObject = Instantiate(playerNameUIPrefab, transform);
        PlayerNameDisplay nameDisplay = nameUIObject.GetComponent<PlayerNameDisplay>();

        if (nameDisplay != null)
        {
            nameDisplay.Initialize(player);
            playerNameDisplays[networkId] = nameDisplay;
            
            Debug.Log($"PlayerNameManager: Created name display for player {networkId}");
        }
        else
        {
            Debug.LogError("PlayerNameManager: PlayerNameUI prefab is missing PlayerNameDisplay component!");
            Destroy(nameUIObject);
        }
    }

    /// <summary>
    /// Update all name display positions to match their players
    /// </summary>
    private void UpdateAllNamePositions()
    {
        Vector3 worldOffset = new Vector3(0, yOffset, 0);
        
        foreach (var kvp in playerNameDisplays)
        {
            PlayerNameDisplay display = kvp.Value;
            if (display != null)
            {
                display.UpdatePosition(mainCamera, worldOffset);
            }
        }
    }

    /// <summary>
    /// Remove name displays for players that no longer exist
    /// </summary>
    private void CleanupDestroyedPlayers()
    {
        List<ulong> toRemove = new List<ulong>();

        foreach (var kvp in playerNameDisplays)
        {
            PlayerNameDisplay display = kvp.Value;
            
            // Check if display or its target is destroyed
            if (display == null || !display.IsTargetValid())
            {
                toRemove.Add(kvp.Key);
                
                // Destroy the display GameObject if it still exists
                if (display != null)
                {
                    Destroy(display.gameObject);
                }
            }
        }

        // Remove from dictionary
        foreach (ulong networkId in toRemove)
        {
            playerNameDisplays.Remove(networkId);
            Debug.Log($"PlayerNameManager: Removed name display for player {networkId}");
        }
    }

    /// <summary>
    /// Manually create a name display for a player (can be called by other scripts)
    /// </summary>
    public void RegisterPlayer(Transform player, ulong networkId)
    {
        if (!playerNameDisplays.ContainsKey(networkId))
        {
            CreateNameDisplayForPlayer(player, networkId);
        }
    }

    /// <summary>
    /// Manually remove a name display (can be called by other scripts)
    /// </summary>
    public void UnregisterPlayer(ulong networkId)
    {
        if (playerNameDisplays.TryGetValue(networkId, out PlayerNameDisplay display))
        {
            if (display != null)
            {
                Destroy(display.gameObject);
            }
            playerNameDisplays.Remove(networkId);
        }
    }

    /// <summary>
    /// Clear all name displays
    /// </summary>
    public void ClearAllDisplays()
    {
        foreach (var kvp in playerNameDisplays)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value.gameObject);
            }
        }
        playerNameDisplays.Clear();
    }

    private void OnDestroy()
    {
        ClearAllDisplays();
        
        if (Instance == this)
        {
            Instance = null;
        }
    }
}

