using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float lookSensitivity = 1.0f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Footstep Settings")]
    [SerializeField] private float footstepInterval = 0.45f;
    [Range(0f, 2f)]
    [SerializeField] private float footstepVolume = 1.0f;
    private float footstepTimer = 0f;

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

    private void Start()
    {
        // Reset movement values on Start to prevent initial frame physics launch
        verticalVelocity = 0f;
        moveInput = Vector2.zero;
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    private void HandleMovement()
    {
        // Cap deltaTime to max 0.05s to prevent massive movement spikes during frame hitches
        float safeDeltaTime = Mathf.Min(Time.deltaTime, 0.05f);

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0)
            {
                verticalVelocity = -2f;
            }
        }

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        verticalVelocity += gravity * safeDeltaTime;

        Vector3 velocity = (move * moveSpeed) + (Vector3.up * verticalVelocity);
        controller.Move(velocity * safeDeltaTime);

        // Footstep timing check
        if (controller.isGrounded && moveInput.sqrMagnitude > 0.01f)
        {
            footstepTimer += Time.deltaTime;
            if (footstepTimer >= footstepInterval)
            {
                TriggerFootstepSound();
                footstepTimer = 0f;
            }
        }
        else
        {
            footstepTimer = 0f;
        }
    }

    private void TriggerFootstepSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFootstep();

            AudioSource playerAudio = GetComponent<AudioSource>();
            if (playerAudio != null)
            {
                playerAudio.volume = footstepVolume;
            }
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