using UnityEngine;

public class SceneViewFlyCamera : MonoBehaviour
{
    [Header("Movement Parameters")]
    // Base movement speed
    public float moveSpeed = 10.0f;
    // Speed multiplier when holding the Shift key
    public float shiftSpeedMultiplier = 2.0f;

    [Header("Mouse Rotation Parameters")]
    // Rotation sensitivity (adjust according to personal preference)
    public float rotationSpeed = 3.0f;

    void Update()
    {
        // ======== 1. Rotate camera with right mouse button ========
        if (Input.GetMouseButton(1))
        {
            // Lock the cursor and hide it when the right mouse button is held down
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Retrieve the mouse movement delta
            float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed;

            // Horizontal rotation: Yaw rotation around the world's Y axis
            transform.Rotate(Vector3.up, mouseX, Space.World);
            // Vertical rotation: Pitch rotation around the object's X axis
            transform.Rotate(Vector3.right, -mouseY, Space.Self);
        }
        else
        {
            // Unlock and show the cursor when the right mouse button is not held
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ======== 2. Keyboard movement control ========
        // Get movement direction (WASD keys for forward/backward and left/right, Q/E for down/up)
        Vector3 direction = Vector3.zero;
        if (Input.GetKey(KeyCode.W))
            direction += transform.forward;
        if (Input.GetKey(KeyCode.S))
            direction -= transform.forward;
        if (Input.GetKey(KeyCode.A))
            direction -= transform.right;
        if (Input.GetKey(KeyCode.D))
            direction += transform.right;
        // Q for down, E for up (keys can be adjusted as needed)
        if (Input.GetKey(KeyCode.E))
            direction += transform.up;
        if (Input.GetKey(KeyCode.Q))
            direction -= transform.up;

        // Normalize the direction vector to avoid faster diagonal movement
        if (direction.magnitude > 1)
            direction.Normalize();

        // ======== 3. Adjust movement speed with Shift key ========
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            speed *= shiftSpeedMultiplier;

        // Apply movement (multiplying by Time.deltaTime to ensure frame-rate independence)
        transform.position += direction * speed * Time.deltaTime;
    }
}
