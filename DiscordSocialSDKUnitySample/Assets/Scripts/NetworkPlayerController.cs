using UnityEngine;
using Unity.Netcode;

/// <summary>
/// NetworkPlayerController extends PlayerController functionality for networked multiplayer.
/// It ensures only the owner can control their player, while remote players are
/// automatically synchronized via NetworkTransform.
/// </summary>
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayerController : NetworkBehaviour
{
    private PlayerController playerController;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        
        if (playerController == null)
        {
            Debug.LogError("NetworkPlayerController: PlayerController component not found!");
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Only enable player control for the owner
        if (playerController != null)
        {
            if (IsOwner)
            {
                playerController.SetCanMove(true);
                Debug.Log($"NetworkPlayerController: Local player spawned. Controls enabled.");
            }
            else
            {
                playerController.SetCanMove(false);
                Debug.Log($"NetworkPlayerController: Remote player spawned. Controls disabled.");
            }
        }

        // Set a distinguishable name for debugging
        if (IsOwner)
        {
            gameObject.name = "Player (Local)";
        }
        else
        {
            gameObject.name = $"Player (Remote-{OwnerClientId})";
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        Debug.Log($"NetworkPlayerController: Player despawned (Network ID: {OwnerClientId})");
    }

    // Allow server to disable/enable movement (for future game mechanics)
    [ServerRpc]
    public void SetCanMoveServerRpc(bool canMove)
    {
        SetCanMoveClientRpc(canMove);
    }

    [ClientRpc]
    private void SetCanMoveClientRpc(bool canMove)
    {
        if (playerController != null && IsOwner)
        {
            playerController.SetCanMove(canMove);
        }
    }

    // Get the underlying PlayerController for other scripts
    public PlayerController GetPlayerController()
    {
        return playerController;
    }
}

