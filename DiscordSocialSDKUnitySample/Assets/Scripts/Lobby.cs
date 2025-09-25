using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

#if DISCORD_SOCIAL_SDK_EXISTS
using Discord.Sdk;
#endif

/// <summary>
/// 
/// </summary>
public class Lobby : MonoBehaviour
{
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button leaveLobbyButton;
    [SerializeField] private string lobbySecret;

#if DISCORD_SOCIAL_SDK_EXISTS
    private Client client;
    private ulong currentLobby;

    void Start()
    {
        client = DiscordManager.Instance.GetClient();
        DiscordManager.Instance.OnDiscordStatusChanged += OnStatusChanged;

        createLobbyButton.onClick.AddListener(CreateLobby);
        leaveLobbyButton.onClick.AddListener(LeaveLobby);

        createLobbyButton.gameObject.SetActive(false);
        leaveLobbyButton.gameObject.SetActive(false);
    }

    private void OnStatusChanged(Client.Status status, Client.Error error, int errorCode)
    {
        if (status == Client.Status.Ready)
        {
            createLobbyButton.gameObject.SetActive(true);
        }
    }

    public void CreateLobby()
    {
        StopAllCoroutines();
        createLobbyButton.gameObject.SetActive(false);
        lobbySecret = System.Guid.NewGuid().ToString();
        client.CreateOrJoinLobby(lobbySecret, OnCreateOrJoinLobby);
    }

    public void JoinLobby(string lobbySecret)
    {
        StopAllCoroutines();
        createLobbyButton.gameObject.SetActive(false);
        StartCoroutine(JoinLobbyCoroutine(lobbySecret));
    }

    private IEnumerator JoinLobbyCoroutine(string lobbySecret)
    {
        yield return new WaitUntil(() => { return client.GetStatus() == Client.Status.Ready; });
        this.lobbySecret = lobbySecret;
        client.CreateOrJoinLobby(this.lobbySecret, OnCreateOrJoinLobby);
    }

    private void OnCreateOrJoinLobby(ClientResult clientResult, ulong lobbyId)
    {
        if (clientResult.Successful())
        {
            currentLobby = lobbyId;

            leaveLobbyButton.gameObject.SetActive(true);

            Debug.Log($"Successfully created or joined lobby {lobbyId}");
        }
        else
        {
            createLobbyButton.gameObject.SetActive(true);

            Debug.LogError($"Failed to create or join lobby: {clientResult}");
        }
    }

    public void LeaveLobby()
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

            createLobbyButton.gameObject.SetActive(true);
        }
        else
        {
            leaveLobbyButton.gameObject.SetActive(true);

            Debug.LogError($"Failed to leave lobby: {clientResult}");
        }
    }
#endif
}
