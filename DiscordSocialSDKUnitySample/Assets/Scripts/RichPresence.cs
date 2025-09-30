using UnityEngine;
#if DISCORD_SOCIAL_SDK_EXISTS
using Discord.Sdk;
#endif

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

        activity.SetType(type);
        activity.SetState(state);
        activity.SetDetails(details);

        var activityTimestamp = new ActivityTimestamps();
        activityTimestamp.SetStart(startTimestamp);
        activity.SetTimestamps(activityTimestamp);

        client.UpdateRichPresence(activity, OnUpdateRichPresence);
    }

    public void SetDefaultRichPresence()
    {
        UpdateRichPresence(ActivityTypes.Playing, startState, startDetails);
    }

    public void UpdateRichPresenceLobby(ActivityTypes type, string state, string details, string lobbySecret, string lobbyId, int maxPartySize)
    {
        Activity activity = new Activity();

        activity.SetType(type);
        activity.SetState(state);
        activity.SetDetails(details);

        ActivityParty party = new ActivityParty();
        party.SetId(lobbyId);
        party.SetCurrentSize(1);
        party.SetMaxSize(maxPartySize);
        activity.SetParty(party);

        ActivitySecrets secrets = new ActivitySecrets();
        secrets.SetJoin(lobbySecret);
        activity.SetSecrets(secrets);

        var activityTimestamp = new ActivityTimestamps();
        activityTimestamp.SetStart(startTimestamp);
        activity.SetTimestamps(activityTimestamp);

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
