using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Nhận input
        float x = Input.GetAxis("Horizontal"); // A D
        float z = Input.GetAxis("Vertical");   // W S

        // Hướng di chuyển
        Vector3 move = transform.right * x + transform.forward * z;

        // Di chuyển
        controller.Move(move * speed * Time.deltaTime);

        // Trọng lực
        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
