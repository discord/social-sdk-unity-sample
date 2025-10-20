using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if DISCORD_SOCIAL_SDK_EXISTS
using Discord.Sdk;
#endif

/// <summary>
/// Invite handles inviting friends to the current lobby when clicked. It will only show the invite button when the current player
/// is in a lobby and hovering over a friend.
/// </summary>
public class Invite : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button inviteButton;
    [SerializeField] private FriendUI friendUI;

    private Lobby lobby;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (lobby != null && lobby.IsInLobby())
        {
            inviteButton.gameObject.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        inviteButton.gameObject.SetActive(false);
    }

#if DISCORD_SOCIAL_SDK_EXISTS
    private Client client;

    void Start()
    {
        inviteButton.onClick.AddListener(InviteFriend);
        inviteButton.gameObject.SetActive(false);

        lobby = FindFirstObjectByType<Lobby>();

        client = DiscordManager.Instance.GetClient();
    }

    private void InviteFriend()
    {
        if (lobby != null && lobby.IsInLobby())
        {
            ulong userId = friendUI.GetUserId();
            client.SendActivityInvite(userId, "Join my lobby!", OnInviteSent);
        }
    }

    private void OnInviteSent(ClientResult result)
    {
        if (result.Successful())
        {
            Debug.Log("Invite successfully sent!");
        }
        else
        {
            Debug.LogError($"Failed to send invite {result.Error()}");
        }
    }
#endif
}
