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
        DiscordManager.Instance.OnDiscordMessageCreated += ShowMessage;
    }

    public void OpenMessager(ulong userId)
    {
        currentUser = userId;
        messagePanel.SetActive(true);
    }

    public void SendDirectMessage(string text)
    {
        client.SendUserMessage(currentUser, text, SendMessageCallback);
    }

    private void SendMessageCallback(ClientResult result, ulong messageId)
    {
        if (result.Successful())
        {
            print("Message sent successfully!");
        }
    }

    private void ShowMessage(ulong messageId)
    {
        MessageHandle message = client.GetMessageHandle(messageId);
        textBox.text += $"{message.Author().DisplayName()}: {message.Content()}\n";
    }
}
