using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

/// <summary>
/// PlayerNetworkData stores and synchronizes player identity information across the network.
/// It uses NetworkVariables to ensure all clients know which Discord user controls which player.
/// </summary>
public class PlayerNetworkData : NetworkBehaviour
{
    [Header("Player Identity")]
    // Store Discord user ID as a NetworkVariable so it syncs across all clients
    private NetworkVariable<ulong> discordUserId = new NetworkVariable<ulong>(
        0, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Owner
    );

    private NetworkVariable<FixedString64Bytes> playerName = new NetworkVariable<FixedString64Bytes>(
        new FixedString64Bytes("Player"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            // Set our Discord user ID when we spawn
#if DISCORD_SOCIAL_SDK_EXISTS
            if (DiscordManager.Instance != null)
            {
                var client = DiscordManager.Instance.GetClient();
                if (client != null)
                {
                    var currentUser = client.GetCurrentUser();
                    discordUserId.Value = currentUser.Id();
                    
                    // Get username
                    string username = currentUser.DisplayName();
                    if (!string.IsNullOrEmpty(username))
                    {
                        playerName.Value = new FixedString64Bytes(username);
                    }
                    
                    Debug.Log($"PlayerNetworkData: Set Discord ID to {discordUserId.Value} for local player");
                }
            }
#endif
        }

        // Subscribe to value changes
        discordUserId.OnValueChanged += OnDiscordUserIdChanged;
        playerName.OnValueChanged += OnPlayerNameChanged;

        Debug.Log($"PlayerNetworkData: Player spawned with Discord ID {discordUserId.Value}, Name: {playerName.Value}");
    }

    public override void OnNetworkDespawn()
    {
        discordUserId.OnValueChanged -= OnDiscordUserIdChanged;
        playerName.OnValueChanged -= OnPlayerNameChanged;
        base.OnNetworkDespawn();
    }

    private void OnDiscordUserIdChanged(ulong previousValue, ulong newValue)
    {
        Debug.Log($"PlayerNetworkData: Discord ID changed from {previousValue} to {newValue}");
        
        // Notify NetworkGameManager about the mapping
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.MapDiscordToNetwork(newValue, OwnerClientId);
        }
    }

    private void OnPlayerNameChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
    {
        Debug.Log($"PlayerNetworkData: Player name changed to {newValue}");
    }

    // Public getters
    public ulong GetDiscordUserId()
    {
        return discordUserId.Value;
    }

    public string GetPlayerName()
    {
        return playerName.Value.ToString();
    }

    // For host/server to set Discord ID for clients
    [ServerRpc]
    public void SetDiscordUserIdServerRpc(ulong userId)
    {
        if (IsServer)
        {
            discordUserId.Value = userId;
        }
    }
}

