using System.Collections.Generic;
using UnityEngine;

public class PlayRandomBirdSounds : MonoBehaviour
{
    public List<AudioClip> birdSounds;
    public AudioSource audioSource;
    public Vector2 randomIntervalRange = new Vector2(5f, 15f);

    void Start()
    {
        StartCoroutine(PlayBirdSoundsRoutine());
    }

    private System.Collections.IEnumerator PlayBirdSoundsRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(randomIntervalRange.x, randomIntervalRange.y);
            yield return new WaitForSeconds(waitTime);

            if (birdSounds.Count > 0 && !audioSource.isPlaying)
            {
                int randomIndex = Random.Range(0, birdSounds.Count);
                audioSource.clip = birdSounds[randomIndex];
                audioSource.Play();
            }
        }
    }
}
