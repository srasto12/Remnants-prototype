using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float jumpForce = 5f;
    private Rigidbody rb;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Freeze X and Z rotation to prevent tipping
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void Update()
    {
        // Get input
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        // Movement vector in world space
        Vector3 move = new Vector3(moveX, 0, moveZ).normalized * speed;

        // Preserve vertical velocity
        Vector3 velocity = move;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;

        // Rotate player to face movement direction
        Vector3 lookDir = new Vector3(moveX, 0, moveZ);
        if (lookDir.sqrMagnitude > 0.01f) // avoid zero vector
        {
            transform.forward = lookDir.normalized;
        }

        // Jump
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        // Simple ground check
        isGrounded = collision.gameObject.CompareTag("Ground");
    }
}
