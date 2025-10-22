using UnityEngine;
using TMPro;
using Unity.Netcode;

/// <summary>
/// PlayerNameDisplay manages a single name UI element for a player.
/// It tracks the player's transform and updates the displayed name from PlayerNetworkData.
/// </summary>
public class PlayerNameDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private CanvasGroup canvasGroup;
    
    private Transform targetPlayer;
    private PlayerNetworkData playerNetworkData;
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// Initialize this name display with a target player
    /// </summary>
    public void Initialize(Transform player)
    {
        targetPlayer = player;
        
        // Get the PlayerNetworkData component
        if (targetPlayer != null)
        {
            playerNetworkData = targetPlayer.GetComponent<PlayerNetworkData>();
            
            if (playerNetworkData != null)
            {
                UpdateNameText();
                
                // Subscribe to name changes if networked
                NetworkObject netObj = targetPlayer.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    // Initial update and listen for future changes
                    StartCoroutine(WaitAndUpdateName());
                }
            }
            else
            {
                Debug.LogWarning("PlayerNameDisplay: Target player has no PlayerNetworkData component");
                SetNameText("Player");
            }
        }
    }

    private System.Collections.IEnumerator WaitAndUpdateName()
    {
        // Wait a frame for network variables to sync
        yield return null;
        UpdateNameText();
    }

    /// <summary>
    /// Update the displayed name from PlayerNetworkData
    /// </summary>
    public void UpdateNameText()
    {
        if (playerNetworkData != null)
        {
            string displayName = playerNetworkData.GetPlayerName();
            
            // Fallback to "Player" if name is empty or default
            if (string.IsNullOrEmpty(displayName) || displayName == "Player")
            {
                displayName = "Player";
            }
            
            SetNameText(displayName);
        }
    }

    /// <summary>
    /// Set the displayed name text directly
    /// </summary>
    public void SetNameText(string name)
    {
        if (nameText != null)
        {
            nameText.text = name;
        }
    }

    /// <summary>
    /// Update the screen position based on target player's world position
    /// </summary>
    public void UpdatePosition(Camera camera, Vector3 worldOffset)
    {
        if (targetPlayer == null || camera == null)
        {
            SetVisible(false);
            return;
        }

        // Calculate world position with offset (above player)
        Vector3 worldPos = targetPlayer.position + worldOffset;
        
        // Convert to screen position
        Vector3 screenPos = camera.WorldToScreenPoint(worldPos);
        
        // Check if position is on screen
        bool isOnScreen = screenPos.z > 0 && 
                         screenPos.x >= 0 && screenPos.x <= Screen.width &&
                         screenPos.y >= 0 && screenPos.y <= Screen.height;
        
        if (isOnScreen)
        {
            rectTransform.position = screenPos;
            SetVisible(true);
        }
        else
        {
            SetVisible(false);
        }
    }

    /// <summary>
    /// Show or hide this name display
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// Get the target player transform
    /// </summary>
    public Transform GetTargetPlayer()
    {
        return targetPlayer;
    }

    /// <summary>
    /// Check if the target player still exists
    /// </summary>
    public bool IsTargetValid()
    {
        return targetPlayer != null;
    }
}

