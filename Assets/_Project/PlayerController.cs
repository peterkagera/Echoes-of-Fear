using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float lookSensitivity = 1.0f;
    [SerializeField] private float gravity = -9.81f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    private CharacterController controller;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float cameraPitch = 0.0f;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();
    }

    public void OnMove(InputValue value)
    {
        //AudioManager.Instance?.PlayFootstep();
        moveInput = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    private void HandleMovement()
    {
        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = (move * moveSpeed) + (Vector3.up * verticalVelocity);
        controller.Move(velocity * Time.deltaTime);

        // Play footstep loop while moving on the ground
        if (controller.isGrounded && moveInput.magnitude > 0.1f)
        {
            AudioManager.Instance?.PlayFootstep();
        }
    }

    private void HandleLook()
    {
        float mouseX = lookInput.x * lookSensitivity * 0.1f;
        float mouseY = lookInput.y * lookSensitivity * 0.1f;

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -89f, 89f);

        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }

        transform.Rotate(Vector3.up * mouseX);
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}