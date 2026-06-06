using UnityEngine;

/// <summary>
/// Thin compatibility shim. All real lobby logic lives in <see cref="GameManager"/>
/// so the proximity-audio demo can be live-coded from one file. Existing scene
/// wiring (Invite, LobbyInviteModal) still calls into this class; it just
/// forwards to the GameManager singleton.
/// </summary>
public class Lobby : MonoBehaviour
{
    public bool IsInLobby() =>
        GameManager.Instance != null && GameManager.Instance.IsInLobby;

    public ulong GetCurrentLobbyId() =>
        GameManager.Instance != null ? GameManager.Instance.CurrentLobbyId : 0;

    public string GetLobbySecret() =>
        GameManager.Instance != null ? GameManager.Instance.LobbySecret : string.Empty;

    public void JoinLobby(string lobbySecret)
    {
#if DISCORD_SOCIAL_SDK_EXISTS
        if (GameManager.Instance != null)
            GameManager.Instance.JoinLobby(lobbySecret);
#endif
    }
}
