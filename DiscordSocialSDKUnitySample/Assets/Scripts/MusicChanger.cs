using UnityEngine;
using Discord.Sdk;

public class MusicChanger : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip linkedClip;
    public AudioClip playingClip;


    void Start()
    {
        DiscordManager.Instance.OnDiscordStatusChanged += OnStatusChanged;
    }

    private void OnStatusChanged(Client.Status status, Client.Error error, int errorCode)
    {
        if (status == Client.Status.Ready)
        {
            float time = audioSource.time;
            audioSource.Stop();
            audioSource.clip = linkedClip;
            audioSource.time = time;
            audioSource.Play();
        }
    }

    public void PlayGame()
    {
            float time = audioSource.time;
            audioSource.Stop();
            audioSource.clip = playingClip;
            audioSource.time = time;
            audioSource.Play();
    }
}
