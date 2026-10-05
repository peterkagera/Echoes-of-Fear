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
    [Tooltip("Reference to the Mobile Interact/Exit Button UI object to ensure it stays active while driving.")]
    [SerializeField] private GameObject mobileInteractButton;

    [Header("Editor Testing Toggles")]
    [Tooltip("Check this to force mobile joysticks active in Editor regardless of active view window.")]
    [SerializeField] private bool enableMobileInEditor = false;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 7.5f;
    [SerializeField] private float gravity = -15.0f;

    [Header("Touch / Joystick Look Settings")]
    [Tooltip("Rotation speed in degrees per second when using touch joysticks.")]
    [SerializeField] private float joystickLookSpeed = 75.0f;
    [Tooltip("Look acceleration response power (higher = finer control in center).")]
    [SerializeField] private float lookExponent = 1.5f;
    [Tooltip("Smoothing factor for touch camera rotation.")]
    [SerializeField] private float lookSmoothing = 25.0f;

    [Header("PC Mouse Look Settings")]
    [Tooltip("Sensitivity multiplier for PC Mouse look.")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [Tooltip("Enable smooth mouse look filtering.")]
    [SerializeField] private bool smoothMouseLook = true;
    [Tooltip("Smoothing speed for PC mouse look (higher = faster response).")]
    [SerializeField] private float mouseSmoothing = 30.0f;

    [Header("Footstep Settings")]
    [SerializeField] private float footstepInterval = 0.4f;
    private float footstepTimer = 0f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Sonar Integration")]
    [Tooltip("Optional reference to your Sonar component/script. If left blank, it will automatically search for it on player and camera.")]
    [SerializeField] private MonoBehaviour sonarController;

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
    private Vector2 currentJoystickLookInput;
    private Vector2 currentMouseDelta;
    private float cameraPitch = 0.0f;
    private float verticalVelocity;
    private bool isMobileActive = false;
    private bool isJoystickMoving = false;
    private TukTukVehicle targetTukTuk;
    private PlayerInteractor playerInteractor;
    public bool IsMobileActive => isMobileActive;
    public Vector2 MoveInput => rawMoveInput;
    public Vector2 LookInput => rawLookInput;
    public bool IsDriving => isDriving;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInteractor = GetComponent<PlayerInteractor>();

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
        currentJoystickLookInput = Vector2.zero;
        currentMouseDelta = Vector2.zero;

        UpdateActiveControlMode();
    }

    private void Update()
    {
#if UNITY_EDITOR
        UpdateEditorControlMode();
#endif

        HandleMobileMovementInput();
        HandleLook();

        if (!isDriving)
        {
            HandleMovement();
        }
        else
        {
            HandleVehicleMovement();

            // Keep Exit button visible while driving on mobile
            if (isMobileActive && mobileInteractButton != null && !mobileInteractButton.activeSelf)
            {
                mobileInteractButton.SetActive(true);
            }
        }
    }

    private void UpdateActiveControlMode()
    {
#if UNITY_EDITOR
        CheckEditorViewMode();
#else
        isMobileActive = UnityEngine.Device.Application.isMobilePlatform;
        SetMobileMode(isMobileActive);
#endif
    }

#if UNITY_EDITOR
    private void UpdateEditorControlMode()
    {
        CheckEditorViewMode();

        if (!isMobileActive && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }
    }

    private void CheckEditorViewMode()
    {
        if (enableMobileInEditor)
        {
            if (!isMobileActive) SetMobileMode(true);
            return;
        }

        bool inSimulator = false;

        var focused = EditorWindow.focusedWindow;
        if (focused != null && focused.GetType().Name.Contains("Simulator"))
        {
            inSimulator = true;
        }

        var mouseOver = EditorWindow.mouseOverWindow;
        if (mouseOver != null && mouseOver.GetType().Name.Contains("Simulator"))
        {
            inSimulator = true;
        }

        if (inSimulator != isMobileActive)
        {
            SetMobileMode(inSimulator);
        }
    }
#endif

    private void SetMobileMode(bool mobileActive)
    {
        isMobileActive = mobileActive;

        if (mobileControlsCanvas != null)
        {
            mobileControlsCanvas.SetActive(isMobileActive);
        }

        if (isMobileActive)
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
        Vector2 input = context.ReadValue<Vector2>();
        if (!isMobileActive || input.sqrMagnitude > 0.01f || context.canceled)
        {
            if (!isJoystickMoving)
            {
                rawMoveInput = input;
            }
        }
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();
        if (!isMobileActive || input.sqrMagnitude > 0.01f || context.canceled)
        {
            rawLookInput = context.ReadValue<Vector2>();
        }
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            OnInteractButtonPressed();
        }
    }

    private void HandleMobileMovementInput()
    {
        if (isMobileActive && movementJoystick != null)
        {
            Vector2 joyInput = movementJoystick.InputVector;
            if (joyInput.sqrMagnitude > 0.001f)
            {
                rawMoveInput = joyInput;
                isJoystickMoving = true;
            }
            else if (isJoystickMoving)
            {
                rawMoveInput = Vector2.zero;
                isJoystickMoving = false;
            }
        }
    }

    private void HandleLook()
    {
        float mouseX = 0f;
        float mouseY = 0f;

        bool isJoystickActive = isMobileActive && lookJoystick != null && lookJoystick.InputVector.sqrMagnitude > 0.001f;

        if (isJoystickActive)
        {
            Vector2 joyInput = lookJoystick.InputVector;
            Vector2 curvedInput = new Vector2(
                Mathf.Sign(joyInput.x) * Mathf.Pow(Mathf.Abs(joyInput.x), lookExponent),
                Mathf.Sign(joyInput.y) * Mathf.Pow(Mathf.Abs(joyInput.y), lookExponent)
            );

            float dampFactor = 1f - Mathf.Exp(-lookSmoothing * Time.deltaTime);
            currentJoystickLookInput = Vector2.Lerp(currentJoystickLookInput, curvedInput, dampFactor);

            mouseX = currentJoystickLookInput.x * joystickLookSpeed * Time.deltaTime;
            mouseY = currentJoystickLookInput.y * joystickLookSpeed * Time.deltaTime;
        }
        else
        {
            Vector2 targetMouseDelta = rawLookInput * mouseSensitivity;

            if (smoothMouseLook)
            {
                float dampFactor = 1f - Mathf.Exp(-mouseSmoothing * Time.deltaTime);
                currentMouseDelta = Vector2.Lerp(currentMouseDelta, targetMouseDelta, dampFactor);
            }
            else
            {
                currentMouseDelta = targetMouseDelta;
            }

            mouseX = currentMouseDelta.x;
            mouseY = currentMouseDelta.y;

            rawLookInput = Vector2.zero;
        }

        if (Mathf.Abs(mouseX) < 0.0001f && Mathf.Abs(mouseY) < 0.0001f) return;

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -89f, 89f);

        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }
        transform.Rotate(0f, mouseX, 0f);
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
            FindTukTukReference(currentVehicle);
            if (targetTukTuk != null)
            {
                targetTukTuk.SetDriveInput(rawMoveInput);
            }
        }
    }

    // Called via Mobile UI Button OnClick() or OnInteract Action
    public void OnInteractButtonPressed()
    {
        SuppressAndCancelSonar();

        if (isDriving)
        {
            if (targetTukTuk == null && currentVehicle != null)
            {
                FindTukTukReference(currentVehicle);
            }

            if (targetTukTuk != null)
            {
                // Attempt standard vehicle exit trigger
                targetTukTuk.Interact();
            }
            else
            {
                // Direct fallback exit if target reference lost
                ExitVehicle();
            }
        }
        else
        {
            if (playerInteractor == null) playerInteractor = GetComponent<PlayerInteractor>();
            if (playerInteractor != null)
            {
                playerInteractor.TriggerInteraction();
            }
        }
    }

    private void FindTukTukReference(GameObject vehicle)
    {
        if (vehicle == null) return;
        targetTukTuk = vehicle.GetComponent<TukTukVehicle>();
        if (targetTukTuk == null) targetTukTuk = vehicle.GetComponentInParent<TukTukVehicle>();
        if (targetTukTuk == null) targetTukTuk = vehicle.GetComponentInChildren<TukTukVehicle>();
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

    public void EnterVehicle(GameObject vehicle)
    {
        SuppressAndCancelSonar();

        currentVehicle = vehicle;
        isDriving = true;

        FindTukTukReference(currentVehicle);

        if (controller != null)
        {
            controller.enabled = false;
        }

        if (playerInteractor != null)
        {
            playerInteractor.enabled = false;
        }

        // Clear and hide HUD interaction text when entering vehicle
        GameObject interactionTextObj = GameObject.Find("InteractionText");
        if (interactionTextObj != null)
        {
            interactionTextObj.SetActive(false);
        }

        if (isMobileActive && mobileInteractButton != null)
        {
            mobileInteractButton.SetActive(true);
        }
    }

    public void ExitVehicle()
    {
        SuppressAndCancelSonar();

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

        if (playerInteractor != null)
        {
            playerInteractor.enabled = true;
        }
    }

    private void SuppressAndCancelSonar()
    {
        gameObject.SendMessage("CancelActiveSonar", SendMessageOptions.DontRequireReceiver);
        gameObject.SendMessage("SuppressSonar", SendMessageOptions.DontRequireReceiver);

        if (cameraTransform != null)
        {
            cameraTransform.gameObject.SendMessage("CancelActiveSonar", SendMessageOptions.DontRequireReceiver);
            cameraTransform.gameObject.SendMessage("SuppressSonar", SendMessageOptions.DontRequireReceiver);
        }

        if (sonarController != null)
        {
            sonarController.SendMessage("CancelActiveSonar", SendMessageOptions.DontRequireReceiver);
            sonarController.SendMessage("SuppressSonar", SendMessageOptions.DontRequireReceiver);
        }
    }

    private void TriggerFootstepSound()
    {
        AudioManager.Instance?.PlayFootstep();
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

    // Called via Mobile UI SonarButton OnClick()
    public void OnSonarButtonPressed()
    {
        if (sonarController != null)
        {
            sonarController.SendMessage("TriggerPing", SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            // Fallback search if sonarController reference wasn't assigned in Inspector
            var sonar = FindAnyObjectByType<SonarPingController>();
            if (sonar != null)
            {
                sonar.TriggerPing();
            }
        }
    }
}