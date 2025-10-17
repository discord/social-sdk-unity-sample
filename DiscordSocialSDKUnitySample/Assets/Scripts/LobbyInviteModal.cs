using UnityEngine;
using TMPro;
using UnityEngine.UI;

#if DISCORD_SOCIAL_SDK_EXISTS
using Discord.Sdk;
#endif

/// <summary>
/// LobbyInviteModal displays a modal when the player receives an activity invite from a friend.
/// It gives them the option to accept or decline the invite. If they accept it will join the lobby associated with the invite.
/// </summary>
public class LobbyInviteModal : MonoBehaviour
{
    [SerializeField] private Button acceptInviteButton;
    [SerializeField] private Button declineInviteButton;
    [SerializeField] private TextMeshProUGUI inviteCodeText;
    [SerializeField] private GameObject inviteModal;
    private Lobby lobby;

#if DISCORD_SOCIAL_SDK_EXISTS
    private Client client;
    private ActivityInvite currentInvite;

    void Start()
    {
        lobby = FindFirstObjectByType<Lobby>();

        client = DiscordManager.Instance.GetClient();

        DiscordManager.Instance.OnDiscordSetActivityInviteCreated += OnActivityInviteCreated;

        acceptInviteButton.onClick.AddListener(AcceptInvite);
        declineInviteButton.onClick.AddListener(DeclineInvite);
    }

    private void OnActivityInviteCreated(ActivityInvite invite)
    {
        currentInvite = invite;
        inviteCodeText.text = $"{client.GetUser(invite.SenderId()).DisplayName()} has invited you to join their game";
        inviteModal.SetActive(true);
    }

    private void AcceptInvite()
    {
        inviteModal.SetActive(false);
        client.AcceptActivityInvite(currentInvite, AcceptInviteCallback);
    }

    private void DeclineInvite()
    {
        inviteModal.SetActive(false);
    }

    private void AcceptInviteCallback(ClientResult result, string lobbySecret)
    {
        if (result.Successful())
        {
            Debug.Log("Successfully joined lobby from invite!");
            lobby.JoinLobby(lobbySecret);
        }
        else
        {
            Debug.LogError($"Failed to join lobby from invite: {result}");
        }
    }
#endif
}
