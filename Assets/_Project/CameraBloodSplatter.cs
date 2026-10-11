using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CameraBloodSplatter : MonoBehaviour
{
    public static CameraBloodSplatter Instance;
    public Image bloodImage;
    public float fadeDuration = 0.8f;

    private Coroutine splashRoutine;
    private RectTransform bloodRect;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (bloodImage != null)
        {
            bloodRect = bloodImage.GetComponent<RectTransform>();
            Color c = bloodImage.color;
            c.a = 0f;
            bloodImage.color = c;
        }
    }

    public void TriggerSplatter()
    {
        if (bloodImage == null || bloodRect == null) return;

        // Keep position locked to center (0,0) so it always covers the screen view
        bloodRect.anchoredPosition = Vector2.zero;

        // Randomize rotation (0, 90, 180, or 270 degrees)
        float randomZ = Random.Range(0, 4) * 90f;
        bloodRect.localRotation = Quaternion.Euler(0f, 0f, randomZ);

        // Scale larger than 1920x1080 (1.8x to 2.3x) so it overflows and fills all edges
        float scale = Random.Range(1.8f, 2.3f);
        float flipX = Random.value > 0.5f ? scale : -scale;
        float flipY = Random.value > 0.5f ? scale : -scale;
        bloodRect.localScale = new Vector3(flipX, flipY, 1f);

        if (splashRoutine != null) StopCoroutine(splashRoutine);
        splashRoutine = StartCoroutine(SplashRoutine());
    }

    private IEnumerator SplashRoutine()
    {
        Color c = bloodImage.color;
        c.a = 1f; // Instant pop on hit

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            bloodImage.color = c;
            yield return null;
        }

        c.a = 0f;
        bloodImage.color = c;
    }
}