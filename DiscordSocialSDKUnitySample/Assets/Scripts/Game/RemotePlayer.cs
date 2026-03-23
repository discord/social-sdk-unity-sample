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

    void Start()
    {
        _targetPosition = transform.position;
        _targetYaw = transform.eulerAngles.y;
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
}
