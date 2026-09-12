using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 7.5f;
    [SerializeField] private float gravity = -15.0f;

    [Header("Mobile Look Settings")]
    [Tooltip("Rotation speed in degrees per second when holding joystick at maximum range.")]
    [SerializeField] private float joystickLookSpeed = 120.0f; // Adjusted for smooth 360 mobile panning

    [Header("Footstep Settings")]
    [SerializeField] private float footstepInterval = 0.4f;
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
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

#if UNITY_ANDROID && !UNITY_EDITOR
        int targetWidth = Screen.width / 2;
        int targetHeight = Screen.height / 2;
        Screen.SetResolution(targetWidth, targetHeight, true);
#endif

        controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        verticalVelocity = -0.5f;
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
    }

    private void Update()
    {
        HandleMovement();
    }

    private void LateUpdate()
    {
        HandleLook();
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
        float safeDeltaTime = Time.deltaTime > 0.05f ? 0.05f : Time.deltaTime;

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0)
            {
                verticalVelocity = -0.75f;
            }
        }
        else
        {
            verticalVelocity += gravity * safeDeltaTime;
        }

        Vector3 move = (transform.right * moveInput.x) + (transform.forward * moveInput.y);
        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        Vector3 velocity = (move * moveSpeed) + (Vector3.up * verticalVelocity);
        controller.Move(velocity * safeDeltaTime);

        if (controller.isGrounded && moveInput.sqrMagnitude > 0.01f)
        {
            footstepTimer += safeDeltaTime;
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
        AudioManager.Instance?.PlayFootstep();
    }

    private void HandleLook()
    {
        // Stop processing look logic immediately when finger releases handle
        if (lookInput.sqrMagnitude < 0.001f) return;

        float safeDeltaTime = Time.deltaTime > 0.05f ? 0.05f : Time.deltaTime;

        // Direct linear scaling: lookInput values range from -1.0 to +1.0
        float mouseX = lookInput.x * joystickLookSpeed * safeDeltaTime;
        float mouseY = lookInput.y * joystickLookSpeed * safeDeltaTime;

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -89f, 89f);

        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }
        transform.Rotate(0f, mouseX, 0f);
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