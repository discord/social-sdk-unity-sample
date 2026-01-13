using UnityEngine;
using Discord.Sdk;
using TMPro;

public class MessageManager : MonoBehaviour
{
    Client client;
    ulong currentUser;

    public GameObject messagePanel;
    public TextMeshProUGUI textBox;

    void Start()
    {
        client = DiscordManager.Instance.GetClient();
        DiscordManager.Instance.OnDiscordMessageCreated += DiscordMessageCreated;
    }

    public void StartDirectMessage(ulong userId)
    {
        currentUser = userId;
        messagePanel.SetActive(true);
    }

    public void SendDirectMessage(string content)
    {
        client.SendUserMessage(currentUser, content, OnSendMessage);
    }

    void OnSendMessage(ClientResult result, ulong messageId)
    {
        if(result.Successful())
        {
            print("Message Sent!");
        }
    }
    
    private void DiscordMessageCreated(ulong messageId)
    {
        MessageHandle message = client.GetMessageHandle(messageId);
        textBox.text += $"{message.Author().DisplayName()}: {message.Content()}\n";
    }
}
