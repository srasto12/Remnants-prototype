using UnityEngine;

public class AutoMoveY : MonoBehaviour
{
    public float distance = 2f;
    public float speed = 2f;
    public float minY = 6f;

    float baseY;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        baseY = minY + distance;
    }

    // Update is called once per frame
    void Update()
    {
        float offset = Mathf.PingPong(Time.time * speed, distance * 2f) - distance;
        float y = baseY + offset;
        var p = transform.position;
        p.y = y;
        transform.position = p;
    }
}
