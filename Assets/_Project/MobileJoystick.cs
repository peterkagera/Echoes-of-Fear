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

    private Vector2 handleStartPosition;

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

        if (handleRect != null)
        {
            handleStartPosition = handleRect.anchoredPosition;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            containerRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            Vector2 offset = localPoint - handleStartPosition;
            InputVector = Vector2.ClampMagnitude(offset / handleRange, 1.0f);

            if (handleRect != null)
            {
                handleRect.anchoredPosition = handleStartPosition + (InputVector * handleRange);
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        InputVector = Vector2.zero;
        if (handleRect != null)
        {
            handleRect.anchoredPosition = handleStartPosition;
        }
    }
}