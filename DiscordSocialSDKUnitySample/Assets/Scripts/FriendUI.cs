using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
#if DISCORD_SOCIAL_SDK_EXISTS
using Discord.Sdk;
#endif

/// <summary>
/// Represents a UI component that displays information about a Discord friend.
/// This class handles rendering user details like username, status, and profile picture.
/// It also manages visual indicators for different online status types (online, idle, DND, offline).
/// </summary>
public class FriendUI : MonoBehaviour
{
    [SerializeField] private TMP_Text username;
    [SerializeField] private TMP_Text status;
    [SerializeField] private Image profileImage;
    [SerializeField] private Image discordFriendImage;
    [SerializeField] private Image statusIndicator;
    [SerializeField] private Sprite onlineSprite;
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite dndSprite;
    [SerializeField] private Sprite offlineSprite;

#if DISCORD_SOCIAL_SDK_EXISTS
    private RelationshipHandle relationshipHandle;

    void Start()
    {
        StartCoroutine(LoadAvatarFromUrl(relationshipHandle.User().AvatarUrl(UserHandle.AvatarType.Png, UserHandle.AvatarType.Png)));
    }

    public void SetUser(RelationshipHandle relationshipHandle)
    {
        this.relationshipHandle = relationshipHandle;
        username.text = relationshipHandle.User().DisplayName();
        discordFriendImage.gameObject.SetActive(relationshipHandle.DiscordRelationshipType() == RelationshipType.Friend);
        UpdateStatus();
    }

    public void UpdateUI()
    {
        UpdateStatus();
    }

    public ulong GetUserId()
    {
        return relationshipHandle.Id();
    }

    private void UpdateStatus()
    {
        status.text = relationshipHandle.User().Status().ToString();
        switch (relationshipHandle.User().Status())
        {
            case StatusType.Online:
                statusIndicator.sprite = onlineSprite;
                break;
            case StatusType.Idle:
                statusIndicator.sprite = idleSprite;
                break;
            case StatusType.Dnd:
                statusIndicator.sprite = dndSprite;
                break;
            default:
                statusIndicator.sprite = offlineSprite;
                break;
        }
    }

    private IEnumerator LoadAvatarFromUrl(string url)
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
