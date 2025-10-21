using UnityEngine;

public class CameraMeander : MonoBehaviour
{
    public float radius = 3.5f;        // max distance from start
    public float speed = 0.2f;         // how quickly the noise moves

    private Vector3 startPos;
    private float seed;

    void Start()
    {
        startPos = transform.localPosition;
        seed = Random.Range(-1000f, 1000f);
    }

    void Update()
    {
        float t = Time.time * speed;
        float tPrev = (Time.time - Time.deltaTime) * speed;

        // Perlin in [0,1] -> shift to [-1,1]
        Vector3 Off(float time)
        {
            float x = (Mathf.PerlinNoise(time, seed) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(seed, time) - 0.5f) * 2f;
            return new Vector3(x * radius, y * radius, -10);
        }

        Vector3 now = Off(t);
        Vector3 prev = Off(tPrev);
        transform.localPosition = startPos + now;
    }
}
