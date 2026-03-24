using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
#if DISCORD_SOCIAL_SDK_EXISTS
using Discord.Sdk;
#endif

/// <summary>
/// FriendsList manages the display of friends in different sections based on their online status.
/// Since Unity doesn't have data binding between UI and variables we need to manually update the UI when we get a callback that a friend has changed status.
/// We also don't want to destroy and recreate the friend UI elements every time a friend comes online or offline, so we keep a dictionary of user IDs to GameObjects.
/// This allows us to update the friend whose status has changed and then update the lists by reparenting the friend UI elements to the correct section based on their current status.
/// 
/// https://discord.com/developers/docs/discord-social-sdk/development-guides/creating-a-unified-friends-list
/// </summary>
public class FriendsList : MonoBehaviour
{
    [SerializeField] private GameObject friendUIPrefab;
    [SerializeField] private Transform inGameFriendList;
    [SerializeField] private FriendSectionUI inGameFriendSection;
    [SerializeField] private Transform onlineFriendList;
    [SerializeField] private FriendSectionUI onlineFriendSection;
    [SerializeField] private Transform offlineFriendList;
    [SerializeField] private FriendSectionUI offlineFriendSection;
    [SerializeField] private RectTransform mainContentRectTransform;

#if DISCORD_SOCIAL_SDK_EXISTS
    private Dictionary<ulong, GameObject> userHandles = new Dictionary<ulong, GameObject>();
    private Client client;

    void Start()
    {
        if (DiscordManager.Instance == null)
        {
            Debug.LogError("There is no DiscordManager instance in the scene. The DiscordManager handles the connection to Discord through the Social SDK. There is a prefab for the DiscordManager in the prefabs folder that you can drop into the scene.");
            return;
        }

        if (GetComponentInParent<Canvas>() == null)
        {
            Debug.LogError($"The ConnectButton must be placed inside a Canvas.");
        }

        if (EventSystem.current == null)
        {
            Debug.LogError("No EventSystem found! Add one to the scene for UI interactions to work properly. Click Game Object > UI > EventSystem to add one to the scene.");
        }

        client = DiscordManager.Instance.GetClient();
        DiscordManager.Instance.OnDiscordStatusChanged += OnStatusChanged;
        DiscordManager.Instance.OnDiscordRelationshipsUpdated += OnRelationshipsUpdated;
    }

    private void OnStatusChanged(Client.Status status, Client.Error error, int errorCode)
    {
        if (status == Client.Status.Ready)
        {
            ClientReady();
        }
    }

    private void ClientReady()
    {
        LoadFriendList();
        SortFriendList();
        UpdateFriendSectionCounts();
        StartCoroutine(RefreshFriendListUICoroutine());
    }

    private void LoadFriendList()
    {
        foreach (RelationshipHandle relationshipHandle in client.GetRelationships())
        {
            if (relationshipHandle.DiscordRelationshipType() != RelationshipType.Friend)
            {
                continue; // Only process friends
            }

            FriendUI friendUI = Instantiate(friendUIPrefab, onlineFriendList).GetComponent<FriendUI>();
            friendUI.SetUser(relationshipHandle);
            userHandles[relationshipHandle.User().Id()] = friendUI.gameObject;
        }
    }

    private void SortFriendList()
    {
        UnparentFriendUI();

        foreach (RelationshipHandle relationshipHandle in client.GetRelationshipsByGroup(RelationshipGroupType.Offline))
        {
            GameObject friendUIObject = userHandles[relationshipHandle.User().Id()];
            if (friendUIObject == null)
            {
                Debug.LogWarning($"Friend UI for user {relationshipHandle.User().Id()} not found in dictionary.");
                continue;
            }
            friendUIObject.transform.SetParent(offlineFriendList, false);
        }
        foreach (RelationshipHandle relationshipHandle in client.GetRelationshipsByGroup(RelationshipGroupType.OnlineElsewhere))
        {
            GameObject friendUIObject = userHandles[relationshipHandle.User().Id()];
            if (friendUIObject == null)
            {
                Debug.LogWarning($"Friend UI for user {relationshipHandle.User().Id()} not found in dictionary.");
                continue;
            }
            friendUIObject.transform.SetParent(onlineFriendList, false);
        }
        foreach (RelationshipHandle relationshipHandle in client.GetRelationshipsByGroup(RelationshipGroupType.OnlinePlayingGame))
        {
            GameObject friendUIObject = userHandles[relationshipHandle.User().Id()];
            if (friendUIObject == null)
            {
                Debug.LogWarning($"Friend UI for user {relationshipHandle.User().Id()} not found in dictionary.");
                continue;
            }
            friendUIObject.transform.SetParent(inGameFriendList, false);
        }
    }

    private void UnparentFriendUI()
    {
        foreach (GameObject friendUI in userHandles.Values)
        {
            friendUI.transform.SetParent(null, false);
        }
    }

    private void OnRelationshipsUpdated(ulong userId)
    {
        if (userHandles.TryGetValue(userId, out GameObject friendUIObject))
        {
            if (friendUIObject == null)
            {
                Debug.LogWarning($"Friend UI for user {userId} {client.GetUser(userId).Username()} not found in dictionary but the key existed???");
                return;
            }
            // Update existing UI
            FriendUI friendUI = friendUIObject.GetComponent<FriendUI>();
            if (friendUI != null)
            {
                friendUI.UpdateUI();
            }
            else
            {
                Debug.LogWarning($"Friend UI component not found on GameObject for user {userId}. Check that it exists on the gameobject.");
            }
        }
        else
        {
            // Create new UI element
            FriendUI fui = Instantiate(friendUIPrefab).GetComponent<FriendUI>();
            fui.SetUser(client.GetRelationshipHandle(userId));
            userHandles[userId] = fui.gameObject;
        }

        SortFriendList();
        UpdateFriendSectionCounts();
        StartCoroutine(RefreshFriendListUICoroutine());
    }

    private void UpdateFriendSectionCounts()
    {
        inGameFriendSection.UpdateSectionFriendCount(inGameFriendList.childCount);
        onlineFriendSection.UpdateSectionFriendCount(onlineFriendList.childCount);
        offlineFriendSection.UpdateSectionFriendCount(offlineFriendList.childCount);
    }

    // Refreshes the UI layout after changes to the friend list.
    // Since the friends list is a set of dynamic scrollable lists that changes as friends come online or offline, we need to force the layout to update the frame after the changes otherwise it won't recalulate the layout properly and you'll see overlap in the lists.
    IEnumerator RefreshFriendListUICoroutine()
    {
        yield return new WaitForFixedUpdate();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(mainContentRectTransform);
    }

    public void ToggleFriendsList()
    {
        print("Hello");
        if(transform.localScale.x < 0.5f)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }
        else
        {
            transform.localScale = new Vector3(0f, 1f, 1f);
        }
    }
#else
    void Start()
    {
        Debug.LogError("The Discord Social SDK package has not been detected. Check out the readme for instructions on how to download and install the package into Unity.");
    }
#endif
}
