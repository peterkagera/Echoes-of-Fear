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

    private void Update()
    {
        CheckForInteractable();

        // Direct key press fallback for Input System
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TriggerInteraction();
        }
    }

    private void CheckForInteractable()
    {
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                currentInteractable = interactable;
                if (promptText != null) promptText.text = $"Press E: {currentInteractable.GetPrompt()}"; return;
            }
        }

        currentInteractable = null;
        if (promptText != null)
        {
            promptText.text = "";
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