using UnityEngine;
using Discord.Sdk;

public class HideWhenLinked : MonoBehaviour
{
    void Start()
    {
        DiscordManager.Instance.OnDiscordStatusChanged += OnStatusChanged;
    }

    private void OnStatusChanged(Client.Status status, Client.Error error, int errorCode)
    {
        if (status == Client.Status.Ready)
        {
            gameObject.SetActive(false);
        }
    }
}
