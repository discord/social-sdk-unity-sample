using UnityEngine;
using Discord.Sdk;

public class SettingsUI : MonoBehaviour
{
    public GameObject settingsPanel;
    public GameObject connectToDiscordPanel;
    public GameObject accountLinkedPanel;
    public GameObject loadingPanel;
    public FriendListAnimation friendsList;

    void Start()
    {
        DiscordManager.Instance.OnDiscordStatusChanged += OnStatusChanged;
    }

    private void OnStatusChanged(Client.Status status, Client.Error error, int errorCode)
    {
        if (status == Client.Status.Ready)
        {
            ShowAccountLinked();
        }
    }

    public void openSettings()
    {
        settingsPanel.SetActive(true);
    }

    public void CloseAll()
    {
        settingsPanel.SetActive(false);
        connectToDiscordPanel.SetActive(false);
        accountLinkedPanel.SetActive(false);
        loadingPanel.SetActive(false);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
    }

    public void OpenDiscord()
    {
        string discordUrl = "https://discord.gg/discord-developers";
        Application.OpenURL(discordUrl);
    }

    public void OpenConnectToDiscord()
    {
        settingsPanel.SetActive(false);
        connectToDiscordPanel.SetActive(true);
    }

    public void CancelConnectToDiscord()
    {
        connectToDiscordPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void StartAuthFlow()
    {
        loadingPanel.SetActive(true);
        connectToDiscordPanel.SetActive(false);
        DiscordManager.Instance.StartOAuthFlow();
    }

    public void ShowAccountLinked()
    {
        loadingPanel.SetActive(false);
        connectToDiscordPanel.SetActive(false);
        settingsPanel.SetActive(false);
        accountLinkedPanel.SetActive(true);
        friendsList.ShowFriendsList();
    }

    public void ShowAccountLinkedOnly()
    {
        accountLinkedPanel.SetActive(true);
    }
    
    public void FinishAccountLinked()
    {
        loadingPanel.SetActive(false);
        connectToDiscordPanel.SetActive(false);
        settingsPanel.SetActive(false);
        accountLinkedPanel.SetActive(false);
    }

    public void AccountLinkCancelled()
    {
        settingsPanel.SetActive(false);
        connectToDiscordPanel.SetActive(true);
        loadingPanel.SetActive(false);
    }
}
