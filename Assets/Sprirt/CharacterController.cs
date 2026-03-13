using UnityEngine;

public class PlayerFPS : MonoBehaviour
{
    [Header("Move")]
    [SerializeField]public float speed = 6f;
    [SerializeField]public float jumpForce = 2f;
    [SerializeField]public float gravity = -9.81f;

    [Header("Mouse Look")]
    [SerializeField]public float mouseSensitivity = 100f;
    [SerializeField]public Transform cameraTransform;

    CharacterController controller;
    float yVelocity;
    float xRotation = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked; // khóa chuột
        Cursor.visible = false;
    }

    void Update()
    {
        Move();
        Look();
    }

    void Move()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = transform.right * h + transform.forward * v;

        if (controller.isGrounded)
        {
            if (yVelocity < 0)
                yVelocity = -2f;

            if (Input.GetButtonDown("Jump"))
                yVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
        }

        yVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * speed;
        velocity.y = yVelocity;

        controller.Move(velocity * Time.deltaTime);
    }

    void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }
}
