using UnityEngine;

public class AutoMoveRotate : MonoBehaviour
{
    public Vector3 rotateAxis = Vector3.up;
    public float speed = 90f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(rotateAxis.normalized * speed * Time.deltaTime);
    }
}
