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

    private Client client;
    private ulong startTimestamp;
    void Start()
    {
        // Get the current time in milliseconds to show how long the player has been in game
        startTimestamp = (ulong)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        client = DiscordManager.Instance.GetClient();
        DiscordManager.Instance.OnDiscordStatusChanged += OnDiscordStatusChanged;
    }

    private void OnDiscordStatusChanged(Client.Status status, Client.Error error, int errorCode)
    {
        if(status == Client.Status.Ready)
        {
            SetRichPresence();
        }
    }

    private void SetRichPresence()
    {
        Activity activity = new Activity();
        activity.SetDetails(startDetails);
        activity.SetState(startState);
        activity.SetType(ActivityTypes.Playing);

        ActivityTimestamps activityTimestamps = new ActivityTimestamps();
        activityTimestamps.SetStart(startTimestamp);

        activity.SetTimestamps(activityTimestamps);

        client.UpdateRichPresence(activity, OnRichPresenceSet);
    }

    private void OnRichPresenceSet(ClientResult result)
    {
        if(result.Successful())
        {
            print("Set Rich Presence!!");
        }
    }
}
