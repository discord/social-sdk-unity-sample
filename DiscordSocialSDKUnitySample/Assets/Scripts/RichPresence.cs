using UnityEngine;
using Discord.Sdk;

/// <summary>
/// RichPresence manages the Discord Rich Presence for the game. You can set game details, state, time played, and more!
/// 
/// https://discord.com/developers/docs/discord-social-sdk/development-guides/setting-rich-presence
/// </summary>
public class RichPresence : MonoBehaviour
{
    [SerializeField] private string startState = "In Unity";
    [SerializeField] private string startDetails = "Creating a game";

#if DISCORD_SOCIAL_SDK_EXISTS
    private Client client;
    private ulong startTimestamp;
    void Start()
    {
        if (DiscordManager.Instance == null)
        {
            Debug.LogError("There is no DiscordManager instance in the scene. The DiscordManager handles the connection to Discord through the Social SDK. There is a prefab for the DiscordManager in the prefabs folder that you can drop into the scene.");
            return;
        }

        client = DiscordManager.Instance.GetClient();
        DiscordManager.Instance.OnDiscordStatusChanged += OnStatusChanged;
    }

    private void OnStatusChanged(Client.Status status, Client.Error error, int errorCode)
    {
        if (status == Client.Status.Ready)
        {
            // Get the current time in milliseconds to show how long the player has been in game
            startTimestamp = (ulong)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            UpdateRichPresence(ActivityTypes.Playing, startState, startDetails);
        }
    }

    public void UpdateRichPresence(ActivityTypes type, string state, string details)
    {
        Activity activity = new Activity();
        activity.SetState(state);
        activity.SetDetails(details);
        activity.SetType(ActivityTypes.Playing);
        
        ActivityTimestamps timestamp = new ActivityTimestamps();
        timestamp.SetStart(startTimestamp);
        activity.SetTimestamps(timestamp);

        client.UpdateRichPresence(activity, OnUpdateRichPresence);
    }

    private void OnUpdateRichPresence(ClientResult result)
    {
        if (result.Successful())
        {
            Debug.Log("Rich presence updated!");
        }
        else
        {
            Debug.LogError($"Failed to update rich presence {result.Error()}");
        }
    }
#endif
}
