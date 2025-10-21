using UnityEngine;

public class FriendListAnimation : MonoBehaviour
{
    public Animator animator;

    public void ShowFriendsList()
    {
        animator.SetTrigger("Open");
    }

    public void HideFriendsList()
    {
        animator.SetTrigger("Close");
    }
}
