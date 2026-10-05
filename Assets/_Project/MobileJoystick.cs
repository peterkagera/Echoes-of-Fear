using UnityEngine;
using UnityEngine.EventSystems;

public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick UI Elements")]
    [SerializeField] private RectTransform containerRect;
    [SerializeField] private RectTransform handleRect;

    [Header("Settings")]
    [SerializeField] private float handleRange = 50f;

    public Vector2 InputVector { get; private set; }

    // Stores the exact point where the user touched down
    private Vector2 pointerDownPosition;
    private Canvas parentCanvas;

    private void Awake()
    {
        if (containerRect == null)
        {
            containerRect = GetComponent<RectTransform>();
        }

        if (handleRect == null && transform.childCount > 0)
        {
            handleRect = transform.GetChild(0).GetComponent<RectTransform>();
        }

        // Cache the parent Canvas reference
        parentCanvas = GetComponentInParent<Canvas>();
    }

    // Safely determines the correct camera parameter based on Canvas Render Mode
    private Camera GetUICamera()
    {
        if (parentCanvas == null) return null;

        // MUST return null if Canvas is Screen Space - Overlay
        return (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? null
            : parentCanvas.worldCamera;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 1. Convert touch position to local space inside container using GetUICamera()
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            containerRect,
            eventData.position,
            GetUICamera(),
            out pointerDownPosition))
        {
            // 2. Snap handle center directly to touch position
            if (handleRect != null)
            {
                handleRect.anchoredPosition = pointerDownPosition;
            }

            OnDrag(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            containerRect,
            eventData.position,
            GetUICamera(),
            out Vector2 localPoint))
        {
            // 3. Measure offset relative to touch start point
            Vector2 offset = localPoint - pointerDownPosition;
            InputVector = Vector2.ClampMagnitude(offset / handleRange, 1.0f);

            if (handleRect != null)
            {
                handleRect.anchoredPosition = pointerDownPosition + (InputVector * handleRange);
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        InputVector = Vector2.zero;
        if (handleRect != null)
        {
            // Reset handle position back to center when released
            handleRect.anchoredPosition = Vector2.zero;
        }
    }
}