using UnityEngine;

public class RandomColor : MonoBehaviour
{
    public Gradient colorGradient;
    SpriteRenderer spriteRenderer;
    
    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = colorGradient.Evaluate(Random.value);
        }
    }
}
