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
    private ulong currentLobbyId = 0;

#if DISCORD_SOCIAL_SDK_EXISTS
    private Client client;

    void Start()
    {
        client = DiscordManager.Instance.GetClient();

        DiscordManager.Instance.OnDiscordLobbyCreated += LobbyCreated;
        DiscordManager.Instance.OnDiscordLobbyDeleted += LobbyDeleted;
        DiscordManager.Instance.OnDiscordLobbyMemberAdded += LobbyMemberAdded;
        DiscordManager.Instance.OnDiscordLobbyMemberRemoved += LobbyMemberRemoved;
    }

    private void LobbyCreated(ulong lobbyId)
    {
        currentLobbyId = lobbyId;

        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }
        lobbyPlayerUIObjects.Clear();
        
        // Get the lobby handle and enumerate ALL existing members
        var lobby = client.GetLobbyHandle(lobbyId);
        var memberIds = lobby.LobbyMemberIds();
        
        Debug.Log($"LobbyList: Lobby created/joined with {memberIds.Length} member(s)");
        
        // Add UI for all members already in the lobby
        foreach (ulong memberId in memberIds)
        {
            LobbyMemberAdded(lobbyId, memberId);
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
        GameObject playerUIObject = Instantiate(lobbyPlayerUIPrefab, content);
        StartCoroutine(LoadAvatarFromUrl(client.GetUser(userId).AvatarUrl(UserHandle.AvatarType.Png, UserHandle.AvatarType.Png), playerUIObject.GetComponentInChildren<Image>()));
        lobbyPlayerUIObjects[userId] = playerUIObject.transform;
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
