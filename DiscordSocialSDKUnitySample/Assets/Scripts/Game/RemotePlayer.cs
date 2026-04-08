using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents another player in the lobby. Call SetTarget() whenever a new
/// position update arrives; the object lerps toward it each frame.
/// </summary>
public class RemotePlayer : MonoBehaviour
{
    [Tooltip("How quickly this object lerps toward the target position.")]
    public float lerpSpeed = 8f;

    private Vector3 _targetPosition;
    private float _targetYaw;
    private bool _hasData;

    public SpriteRenderer mouthSprite;
    public List<Sprite> mouthSprites;

    void Start()
    {
        _targetPosition = transform.position;
        _targetYaw = transform.eulerAngles.y;
        mouthSprite.enabled = false;
    }

    void Update()
    {
        if (!_hasData) return;

        transform.position = Vector3.Lerp(transform.position, _targetPosition, lerpSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            Quaternion.Euler(0f, _targetYaw, 0f),
            lerpSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Updates the target position and yaw. On the very first call the object
    /// snaps to the position so it doesn't slide in from the spawn point.
    /// </summary>
    public void SetTarget(Vector3 pos, float yaw)
    {
        if (!_hasData)
        {
            transform.position = pos;
            _hasData = true;
        }
        _targetPosition = pos;
        _targetYaw = yaw;
    }

    /// <summary>
    /// Called by GameManager when Discord reports this player started or stopped speaking.
    /// </summary>
    public void SetSpeaking(bool isSpeaking)
    {
        if (isSpeaking)
            OnSpeakingStart();
        else
            OnSpeakingStop();
    }

    private void OnSpeakingStart()
    {
        mouthSprite.enabled = true;
        StartCoroutine(SpeakingCoroutine());
    }

    private IEnumerator SpeakingCoroutine()
    {
        while (true)
        {
            mouthSprite.sprite = mouthSprites[Random.Range(0, mouthSprites.Count)];
            yield return new WaitForSeconds(0.15f);
        }
    }

    private void OnSpeakingStop()
    {
        StopAllCoroutines();
        mouthSprite.enabled = false;
    }
}
