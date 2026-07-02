using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

#if DISCORD_SOCIAL_SDK_EXISTS
using Discord.Sdk;
#endif

/// <summary>
/// LobbyList manages the UI list of players currently in the lobby. It loads and displays each player's avatar and keeps the list updated
/// as players join or leave the lobby.
/// </summary>
public class LobbyList : MonoBehaviour
{
    [SerializeField] private GameObject lobbyPlayerUIPrefab;
    [SerializeField] private Transform content;
    private Dictionary<ulong, Transform> lobbyPlayerUIObjects = new Dictionary<ulong, Transform>();

#if DISCORD_SOCIAL_SDK_EXISTS
    private Client client;

    void Start()
    {
        client = DiscordManager.Instance.GetClient();

        DiscordManager.Instance.OnDiscordLobbyCreated += LobbyCreatedOrJoined;
        DiscordManager.Instance.OnDiscordLobbyDeleted += LobbyDeleted;
        DiscordManager.Instance.OnDiscordLobbyMemberAdded += LobbyMemberAdded;
        DiscordManager.Instance.OnDiscordLobbyMemberRemoved += LobbyMemberRemoved;
    }

    private void LobbyCreatedOrJoined(ulong lobbyId)
    {
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }
        lobbyPlayerUIObjects.Clear();

        LobbyHandle lobby = client.GetLobbyHandle(lobbyId);
        if (lobby == null)
        {
            return;
        }

        foreach (ulong userId in lobby.LobbyMemberIds())
        {
            LobbyMemberAdded(lobbyId, userId);
        }
    }

    private void LobbyDeleted(ulong lobbyId)
    {
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }
        lobbyPlayerUIObjects.Clear();
    }

    private void LobbyMemberAdded(ulong lobbyId, ulong userId)
    {
        if (lobbyPlayerUIObjects.ContainsKey(userId))
        {
            return;
        }

        GameObject playerUIObject = Instantiate(lobbyPlayerUIPrefab, content);
        lobbyPlayerUIObjects[userId] = playerUIObject.transform;

        UserHandle user = client.GetLobbyHandle(lobbyId)?.GetLobbyMemberHandle(userId)?.User();
        if (user != null)
        {
            StartCoroutine(LoadAvatarFromUrl(user.AvatarUrl(UserHandle.AvatarType.Png, UserHandle.AvatarType.Png), playerUIObject.GetComponentInChildren<Image>()));
        }
        else
        {
            Debug.LogWarning($"No UserHandle available for lobby member {userId}; avatar will not be loaded.");
        }
    }

    private void LobbyMemberRemoved(ulong lobbyId, ulong userId)
    {
        if (lobbyPlayerUIObjects.TryGetValue(userId, out Transform playerUITransform))
        {
            Destroy(playerUITransform.gameObject);
            lobbyPlayerUIObjects.Remove(userId);
        }
    }

    private IEnumerator LoadAvatarFromUrl(string url, Image profileImage)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                profileImage.sprite = sprite;
            }
            else
            {
                Debug.LogError($"Failed to load profile image from URL: {url}. Error: {request.error}");
            }
        }
    }
#endif
}
