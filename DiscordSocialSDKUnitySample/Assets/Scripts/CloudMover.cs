using UnityEngine;

public class CloudMover : MonoBehaviour
{
    public float speed = 0.5f;

    void FixedUpdate()
    {
        transform.position += Vector3.right * Time.fixedDeltaTime * speed;
    }
}
