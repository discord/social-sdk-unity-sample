using System.Collections.Generic;
using UnityEngine;
#if DISCORD_SOCIAL_SDK_EXISTS
using Discord.Sdk;
#endif

/// <summary>
/// ShowOnConnect activates one or more GameObjects once the Discord Social SDK
/// successfully connects (when the client status becomes Ready).
///
/// Place this on a parent object that stays active, and drag the GameObjects you
/// want to reveal into the objectsToShow list in the inspector. They should start
/// inactive in the scene so they only appear after a successful connection.
/// </summary>
public class ShowOnConnect : MonoBehaviour
{
    [Tooltip("GameObjects to activate once the Social SDK reports a Ready status.")]
    [SerializeField] private List<GameObject> objectsToShow = new List<GameObject>();

#if DISCORD_SOCIAL_SDK_EXISTS
    void Start()
    {
        if (DiscordManager.Instance == null)
        {
            Debug.LogError("There is no DiscordManager instance in the scene. The DiscordManager handles the connection to Discord through the Social SDK. There is a prefab for the DiscordManager in the prefabs folder that you can drop into the scene.");
            return;
        }

        foreach(GameObject obj in objectsToShow)
        {
            obj.SetActive(false);
        }

        DiscordManager.Instance.OnDiscordStatusChanged += OnStatusChanged;
    }

    void OnDestroy()
    {
        if (DiscordManager.Instance != null)
        {
            DiscordManager.Instance.OnDiscordStatusChanged -= OnStatusChanged;
        }
    }

    private void OnStatusChanged(Client.Status status, Client.Error error, int errorCode)
    {
        if (status != Client.Status.Ready)
        {
            return;
        }

        foreach (GameObject obj in objectsToShow)
        {
            if (obj != null)
            {
                obj.SetActive(true);
            }
        }
    }
#endif
}
