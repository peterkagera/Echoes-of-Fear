using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Raycast Settings")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float interactDistance = 3.0f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private TextMeshProUGUI promptText;

    private IInteractable currentInteractable;
    private float raycastTimer = 0f;
    private const float RaycastInterval = 0.1f; // 10 checks/sec instead of 60+
    private string lastPromptText = "";

    private void Update()
    {
        raycastTimer += Time.deltaTime;
        if (raycastTimer >= RaycastInterval)
        {
            raycastTimer = 0f;
            CheckForInteractable();
        }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TriggerInteraction();
        }
    }

    private void CheckForInteractable()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                currentInteractable = interactable;
                UpdatePromptText($"Press E: {currentInteractable.GetPrompt()}");
                return;
            }
        }

        currentInteractable = null;
        UpdatePromptText("");
    }

    private void UpdatePromptText(string newText)
    {
        if (promptText != null && lastPromptText != newText)
        {
            promptText.text = newText;
            lastPromptText = newText;
        }
    }

    public void OnInteract(InputValue value)
    {
        if (value.isPressed)
        {
            TriggerInteraction();
        }
    }

    private void TriggerInteraction()
    {
        if (currentInteractable != null)
        {
            currentInteractable.Interact();
        }
    }
}