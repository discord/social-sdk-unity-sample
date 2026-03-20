using UnityEngine;

public class Revive : MonoBehaviour
{
    void OnTriggerExit(Collider other)
    {
        PlayerMovement playerMovement = other.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.transform.position = transform.position;
        }
    }
}
