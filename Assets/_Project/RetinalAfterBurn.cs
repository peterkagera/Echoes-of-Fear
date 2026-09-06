using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class RetinalAfterBurn : MonoBehaviour
{
    public static RetinalAfterBurn Instance { get; private set; }

    [Header("UI Overlay References")]
    [Tooltip("A full-screen UI RawImage assigned to a Canvas set to Screen Space - Overlay.")]
    [SerializeField] private RawImage afterBurnOverlay;
    [SerializeField] private CanvasGroup overlayCanvasGroup;

    [Header("After-Burn Tuning")]
    [SerializeField] private float burnFadeDuration = 2.5f;
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    [SerializeField] private Color burnGlowColor = new Color(0.2f, 0.9f, 1.0f, 0.85f);

    private RenderTexture capturedFrame;
    private Camera targetCamera;
    private Coroutine activeBurnCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        targetCamera = GetComponent<Camera>();

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.blocksRaycasts = false;
        }
    }

    public void CaptureAfterBurn()
    {
        if (targetCamera == null || afterBurnOverlay == null) return;

        if (activeBurnCoroutine != null)
        {
            StopCoroutine(activeBurnCoroutine);
        }

        activeBurnCoroutine = StartCoroutine(RenderAndFadeSequence());
    }

    private IEnumerator RenderAndFadeSequence()
    {
        yield return new WaitForEndOfFrame();

        if (capturedFrame != null) capturedFrame.Release();
        capturedFrame = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);

        targetCamera.targetTexture = capturedFrame;
        targetCamera.Render();
        targetCamera.targetTexture = null;

        afterBurnOverlay.texture = capturedFrame;
        afterBurnOverlay.color = burnGlowColor;

        float elapsedTime = 0f;
        while (elapsedTime < burnFadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / burnFadeDuration;

            if (overlayCanvasGroup != null)
            {
                overlayCanvasGroup.alpha = fadeCurve.Evaluate(progress);
            }

            yield return null;
        }

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 0f;
        }

        if (capturedFrame != null)
        {
            capturedFrame.Release();
            capturedFrame = null;
        }
    }
}