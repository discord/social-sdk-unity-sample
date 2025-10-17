using UnityEngine;
#if DISCORD_SOCIAL_SDK_EXISTS
using Discord.Sdk;
#endif

/// <summary>
/// DiscordManager handles the integration with Discord's Social SDK.
/// It provides functionality for OAuth authentication and managing relationships.
/// It is a singleton that can be accessed from other scripts using DiscordManager.Instance
/// 
/// Ensure you have the Discord Social SDK configured correctly in your project.
/// This script should be attached to a GameObject in your scene, and a Button
/// for connecting to Discord should be linked via the inspector.
/// </summary>
public class DiscordManager : MonoBehaviour
{
    [SerializeField] private DiscordSocialSDKConfig discordSocialSDKConfig;

#if DISCORD_SOCIAL_SDK_EXISTS
    public static DiscordManager Instance { get; private set; }
    private Client client;
    private string codeVerifier;

    // The Discord Social SDK callbacks only support a single callback.
    // In order to allow multiple listeners to subscribe to these callbacks in Unity we create delegates and
    // invoke those when the callbacks are triggered.
    // This lets us modularly subscribe to these callbacks frommany different scripts.
    public delegate void StatusChangedHandler(Client.Status status, Client.Error error, int errorCode);
    public event StatusChangedHandler OnDiscordStatusChanged;

    public delegate void RelationshipsUpdatedHandler(ulong userId);
    public event RelationshipsUpdatedHandler OnDiscordRelationshipsUpdated;

    public delegate void LobbyCreatedHandler(ulong lobbyId);
    public event LobbyCreatedHandler OnDiscordLobbyCreated;

    public delegate void LobbyDeletedHandler(ulong lobbyId);
    public event LobbyDeletedHandler OnDiscordLobbyDeleted;

    public delegate void LobbyMemberAddedHandler(ulong lobbyId, ulong userId);
    public event LobbyMemberAddedHandler OnDiscordLobbyMemberAdded;

    public delegate void LobbyMemberRemovedHandler(ulong lobbyId, ulong userId);
    public event LobbyMemberRemovedHandler OnDiscordLobbyMemberRemoved;

    public delegate void SetActivityInviteCreatedHandler(ActivityInvite invite);
    public event SetActivityInviteCreatedHandler OnDiscordSetActivityInviteCreated;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }

        client = new Client();

        client.AddLogCallback(OnLog, LoggingSeverity.Error);
        client.SetStatusChangedCallback(OnStatusChanged);
        client.SetRelationshipGroupsUpdatedCallback(OnRelationshipsUpdated);
        client.SetLobbyCreatedCallback(OnLobbyCreated);
        client.SetLobbyDeletedCallback(OnLobbyDeleted);
        client.SetLobbyMemberAddedCallback(OnLobbyMemberAdded);
        client.SetLobbyMemberRemovedCallback(OnLobbyMemberRemoved);
        client.SetActivityInviteCreatedCallback(OnSetActivityInviteCreated);
    }

    void Start()
    {
        // Registering an empty launch command will register the current running executible/app in Windows, Mac, and Linux
        client.RegisterLaunchCommand(discordSocialSDKConfig.ApplicationId, string.Empty);

        if (PlayerPrefs.HasKey("RefreshToken"))
        {
            client.RefreshToken(discordSocialSDKConfig.ApplicationId, PlayerPrefs.GetString("RefreshToken"), OnGetToken);
        }
    }

    private void OnDestroy()
    {
        client.Disconnect();
    }

    public Client GetClient()
    {
        return client;
    }

    private void OnLog(string message, LoggingSeverity severity)
    {
        Debug.Log($"Log: {severity} - {message}");
    }

    private void OnStatusChanged(Client.Status status, Client.Error error, int errorCode)
    {
        Debug.Log($"Status changed: {status}");

        if (OnDiscordStatusChanged != null)
        {
            OnDiscordStatusChanged.Invoke(status, error, errorCode);
        }

        if (error != Client.Error.None)
        {
            Debug.LogError($"Error: {error}, code: {errorCode}");
        }
    }

    private void OnRelationshipsUpdated(ulong userId)
    {
        if (OnDiscordRelationshipsUpdated != null)
        {
            OnDiscordRelationshipsUpdated.Invoke(userId);
        }
    }

    private void OnLobbyCreated(ulong lobbyId)
    {
        if (OnDiscordLobbyCreated != null)
        {
            OnDiscordLobbyCreated.Invoke(lobbyId);
        }
    }

    private void OnLobbyDeleted(ulong lobbyId)
    {
        if (OnDiscordLobbyDeleted != null)
        {
            OnDiscordLobbyDeleted.Invoke(lobbyId);
        }
    }

    private void OnLobbyMemberAdded(ulong lobbyId, ulong userId)
    {
        if (OnDiscordLobbyMemberAdded != null)
        {
            OnDiscordLobbyMemberAdded.Invoke(lobbyId, userId);
        }
    }

    private void OnLobbyMemberRemoved(ulong lobbyId, ulong userId)
    {
        if (OnDiscordLobbyMemberRemoved != null)
        {
            OnDiscordLobbyMemberRemoved.Invoke(lobbyId, userId);
        }
    }

    private void OnSetActivityInviteCreated(ActivityInvite invite)
    {
        if (OnDiscordSetActivityInviteCreated != null)
        {
            OnDiscordSetActivityInviteCreated.Invoke(invite);
        }
    }

    public void StartOAuthFlow()
    {
        if (discordSocialSDKConfig == null)
        {
            Debug.LogError("Discord Social SDK Config is not set. Create one in your asset folder by right-clicking and selecting Create > Config > Discord Social SDK and attaching it to the DiscordManager.");
            return;
        }
        if (discordSocialSDKConfig.ApplicationId == 0)
        {
            Debug.LogError("Discord Social SDK Config Application ID is not set. Make sure you've created an app in the Discord developer portal and copied the Application ID into the config.");
            return;
        }

        var authorizationVerifier = client.CreateAuthorizationCodeVerifier();
        codeVerifier = authorizationVerifier.Verifier();

        var args = new AuthorizationArgs();
        args.SetClientId(discordSocialSDKConfig.ApplicationId);
        args.SetScopes(Client.GetDefaultCommunicationScopes());
        args.SetCodeChallenge(authorizationVerifier.Challenge());
        client.Authorize(args, OnAuthorizeResult);
    }


    private void OnAuthorizeResult(ClientResult result, string code, string redirectUri)
    {
        if (!result.Successful())
        {
            Debug.Log($"Authorization result: [{result.Error()}]");
            return;
        }
        GetTokenFromCode(code, redirectUri);
    }

    private void GetTokenFromCode(string code, string redirectUri)
    {
        client.GetToken(discordSocialSDKConfig.ApplicationId, code, codeVerifier, redirectUri, OnGetToken);
    }

    private void OnGetToken(ClientResult result, string token, string refreshToken, AuthorizationTokenType tokenType, int expiresIn, string scope)
    {
        // Storing the refresh token like this in plain text is not secure. You will want to find a cryptography library for your platform to use to encrypt the token before storing it.
        // This is ok for testing purposes with a Public Client but when you have a server for your game the OAuth flow should be handled server-side instead of client-side like this.
        PlayerPrefs.SetString("RefreshToken", refreshToken);

        if (token == null || token == string.Empty)
        {
            Debug.Log("Failed to retrieve token");
        }
        else
        {
            client.UpdateToken(AuthorizationTokenType.Bearer, token, OnUpdateToken);
        }
    }

    private void OnUpdateToken(ClientResult result)
    {
        if (result.Successful())
        {
            client.Connect();
        }
        else
        {
            Debug.LogError($"Failed to update token: {result.Error()}");
        }
    }
#endif
}