using UnityEngine;
using UnityEngine.UI;
using System.Collections;


#if DISCORD_SOCIAL_SDK_EXISTS
using Discord.Sdk;
#endif

/// <summary>
/// Lobby handles creating, joining, and leaving Discord Lobbies. Through the UI the player can create or leave a lobby.
/// Joining a lobby directly happens through invites and Rich Presence.
/// </summary>
public class Lobby : MonoBehaviour
{
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button leaveLobbyButton;
    [SerializeField] private int maxLobbySize = 4;
    private string lobbySecret = string.Empty;
    private ulong currentLobby = 0;

#if DISCORD_SOCIAL_SDK_EXISTS
    private Client client;
    private RichPresence richPresence;

    void Start()
    {
        richPresence = FindFirstObjectByType<RichPresence>();

        client = DiscordManager.Instance.GetClient();
        DiscordManager.Instance.OnDiscordStatusChanged += OnStatusChanged;

        // createLobbyButton.onClick.AddListener(CreateLobby);
        // leaveLobbyButton.onClick.AddListener(LeaveLobby);

        // createLobbyButton.gameObject.SetActive(false);
        // leaveLobbyButton.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if(client != null)
        {
            client.LeaveLobby(currentLobby, (ClientResult result) => { });
        }
    }

    public bool IsInLobby()
    {
        return lobbySecret != string.Empty;
    }

    public ulong GetCurrentLobbyId()
    {
        return currentLobby;
    }

    private void OnStatusChanged(Client.Status status, Client.Error error, int errorCode)
    {
        if (status == Client.Status.Ready)
        {
            // createLobbyButton.gameObject.SetActive(true);
        }
    }

    public void CreateLobby()
    {
        StopAllCoroutines();
        // createLobbyButton.gameObject.SetActive(false);
        lobbySecret = System.Guid.NewGuid().ToString();
        client.CreateOrJoinLobby(lobbySecret, OnCreateOrJoinLobby);
    }

    public void JoinLobby(string lobbySecret)
    {
        // createLobbyButton.gameObject.SetActive(false);
        this.lobbySecret = lobbySecret;
        client.CreateOrJoinLobby(this.lobbySecret, OnCreateOrJoinLobby);
    }

    private void OnCreateOrJoinLobby(ClientResult clientResult, ulong lobbyId)
    {
        if (clientResult.Successful())
        {
            currentLobby = lobbyId;

            leaveLobbyButton.gameObject.SetActive(true);

            if(richPresence != null)
            {
                richPresence.UpdateRichPresenceLobby(ActivityTypes.Playing, "In Lobby", "Waiting for players", lobbySecret, lobbyId.ToString(), maxLobbySize);
            }

            Debug.Log($"Successfully created or joined lobby {lobbyId}");
        }
        else
        {
            // createLobbyButton.gameObject.SetActive(true);

            Debug.LogError($"Failed to create or join lobby: {clientResult}");
        }
    }

    private void LeaveLobby()
    {
        leaveLobbyButton.gameObject.SetActive(false);

        client.LeaveLobby(currentLobby, OnLeaveLobby);
    }

    private void OnLeaveLobby(ClientResult clientResult)
    {
        if (clientResult.Successful())
        {
            Debug.Log($"Successfully left lobby {currentLobby}");

            currentLobby = 0;
            lobbySecret = string.Empty;

            // createLobbyButton.gameObject.SetActive(true);

            if(richPresence != null)
            {
                richPresence.SetDefaultRichPresence();
            }
        }
        else
        {
            leaveLobbyButton.gameObject.SetActive(true);

            Debug.LogError($"Failed to leave lobby: {clientResult}");
        }
    }
#endif
}
