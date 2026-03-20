using UnityEngine;

public class BillboardSprite : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;

    void Start()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    void Update()
    {
        if (Camera.main != null)
        {
            Vector3 cameraPosition = Camera.main.transform.position;
            Vector3 directionToCamera = cameraPosition - transform.position;
            directionToCamera.y = 0; 
            transform.rotation = Quaternion.LookRotation(directionToCamera);
        }
    }
}
