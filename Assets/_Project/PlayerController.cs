using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Mobile UI References")]
    [SerializeField] private MobileJoystick movementJoystick;
    [SerializeField] private MobileJoystick lookJoystick;
    [SerializeField] private GameObject mobileControlsCanvas;

    [Header("Editor Testing Toggles")]
    [Tooltip("Check this to force mobile joysticks active while testing in the Editor / Simulator window.")]
    [SerializeField] private bool enableMobileInEditor = true;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 7.5f;
    [SerializeField] private float gravity = -15.0f;

    [Header("Look Settings")]
    [Tooltip("Rotation speed in degrees per second when using touch joysticks.")]
    [SerializeField] private float joystickLookSpeed = 75.0f;
    [Tooltip("Look acceleration response power (higher = finer control in center).")]
    [SerializeField] private float lookExponent = 1.5f;
    [Tooltip("Smoothing factor for touch camera rotation.")]
    [SerializeField] private float lookSmoothing = 25.0f;
    [Tooltip("Sensitivity multiplier when testing with a PC Mouse in the Game view.")]
    [SerializeField] private float mouseSensitivity = 0.15f;

    [Header("Footstep Settings")]
    [SerializeField] private float footstepInterval = 0.4f;
    private float footstepTimer = 0f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Flashlight Settings")]
    [Tooltip("Direct reference to the FlashlightController script on the FlashLight GameObject.")]
    [SerializeField] private FlashlightController flashlightController;
    [SerializeField] private Light flashlightComponent;

    [Header("Vehicle Integration")]
    [SerializeField] private GameObject currentVehicle;
    [SerializeField] private bool isDriving = false;

    private CharacterController controller;
    private Vector2 rawMoveInput;
    private Vector2 rawLookInput;
    private Vector2 currentLookInput;
    private float cameraPitch = 0.0f;
    private float verticalVelocity;
    private bool isMobileActive = false;
    private TukTukVehicle targetTukTuk;

    public Vector2 MoveInput => rawMoveInput;
    public Vector2 LookInput => rawLookInput;
    public bool IsDriving => isDriving;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (flashlightController == null && cameraTransform != null)
        {
            flashlightController = cameraTransform.GetComponentInChildren<FlashlightController>();
        }

        if (flashlightComponent == null && cameraTransform != null)
        {
            flashlightComponent = cameraTransform.GetComponentInChildren<Light>();
        }
    }

    private void Start()
    {
        verticalVelocity = -2.0f;
        rawMoveInput = Vector2.zero;
        rawLookInput = Vector2.zero;
        currentLookInput = Vector2.zero;

#if UNITY_EDITOR
        isMobileActive = enableMobileInEditor;
#else
        isMobileActive = true;
#endif
        SetMobileMode(isMobileActive);
    }

    private void Update()
    {
        if (isMobileActive)
        {
            if (movementJoystick != null)
            {
                rawMoveInput = movementJoystick.InputVector;
            }

            if (lookJoystick != null)
            {
                rawLookInput = lookJoystick.InputVector;
            }
        }

        HandleLook();

        if (!isDriving)
        {
            HandleMovement();
        }
        else
        {
            HandleVehicleMovement();
        }
    }

    /// <summary>
    /// Mobile UI Event: 
    /// Contextually handles both interacting/entering objects when walking AND exiting vehicles when driving.
    /// </summary>
    public void OnInteractButtonPressed()
    {
        if (isDriving)
        {
            if (targetTukTuk != null)
            {
                targetTukTuk.Interact();
            }
            else if (currentVehicle != null && currentVehicle.TryGetComponent<TukTukVehicle>(out var tukTuk))
            {
                tukTuk.Interact();
            }
        }
        else
        {
            if (TryGetComponent<PlayerInteractor>(out var interactor))
            {
                interactor.TriggerInteraction();
            }
        }
    }

    private void SetMobileMode(bool mobileActive)
    {
        if (mobileControlsCanvas != null)
        {
            mobileControlsCanvas.SetActive(mobileActive);
        }

        if (mobileActive)
        {
            UnlockCursor();
        }
        else
        {
            LockCursor();
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!isMobileActive)
        {
            rawMoveInput = context.ReadValue<Vector2>();
        }
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        if (!isMobileActive)
        {
            rawLookInput = context.ReadValue<Vector2>();
        }
    }

    public void OnFlashlightInput(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            ToggleFlashlight();
        }
    }

    public void ToggleFlashlight()
    {
        if (flashlightController != null)
        {
            flashlightController.ToggleFlashlight();
        }
        else if (flashlightComponent != null)
        {
            flashlightComponent.enabled = !flashlightComponent.enabled;
        }
    }

    private void HandleMovement()
    {
        if (controller == null || !controller.enabled) return;

        if (controller.isGrounded)
        {
            verticalVelocity = -2.0f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 move = (transform.right * rawMoveInput.x) + (transform.forward * rawMoveInput.y);
        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        Vector3 velocity = (move * moveSpeed) + (Vector3.up * verticalVelocity);
        controller.Move(velocity * Time.deltaTime);

        if (controller.isGrounded && rawMoveInput.sqrMagnitude > 0.01f)
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

    private void HandleVehicleMovement()
    {
        if (targetTukTuk != null)
        {
            targetTukTuk.SetDriveInput(rawMoveInput);
        }
        else if (currentVehicle != null)
        {
            if (currentVehicle.TryGetComponent<TukTukVehicle>(out var tukTuk))
            {
                targetTukTuk = tukTuk;
                targetTukTuk.SetDriveInput(rawMoveInput);
            }
        }
    }

    private void TriggerFootstepSound()
    {
        AudioManager.Instance?.PlayFootstep();
    }

    private void HandleLook()
    {
        float mouseX, mouseY;

        if (isMobileActive)
        {
            Vector2 curvedInput = new Vector2(
                Mathf.Sign(rawLookInput.x) * Mathf.Pow(Mathf.Abs(rawLookInput.x), lookExponent),
                Mathf.Sign(rawLookInput.y) * Mathf.Pow(Mathf.Abs(rawLookInput.y), lookExponent)
            );

            float dampFactor = 1f - Mathf.Exp(-lookSmoothing * Time.deltaTime);
            currentLookInput = Vector2.Lerp(currentLookInput, curvedInput, dampFactor);

            if (currentLookInput.sqrMagnitude < 0.0001f) return;

            mouseX = currentLookInput.x * joystickLookSpeed * Time.deltaTime;
            mouseY = currentLookInput.y * joystickLookSpeed * Time.deltaTime;
        }
        else
        {
            if (rawLookInput.sqrMagnitude < 0.001f) return;

            mouseX = rawLookInput.x * mouseSensitivity;
            mouseY = rawLookInput.y * mouseSensitivity;
        }

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -89f, 89f);

        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }
        transform.Rotate(0f, mouseX, 0f);
    }

    public void EnterVehicle(GameObject vehicle)
    {
        currentVehicle = vehicle;
        isDriving = true;

        if (currentVehicle != null)
        {
            targetTukTuk = currentVehicle.GetComponent<TukTukVehicle>();
        }

        if (controller != null)
        {
            controller.enabled = false;
        }
    }

    public void ExitVehicle()
    {
        if (targetTukTuk != null)
        {
            targetTukTuk.SetDriveInput(Vector2.zero);
        }

        currentVehicle = null;
        targetTukTuk = null;
        isDriving = false;

        if (controller != null)
        {
            controller.enabled = true;
        }
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