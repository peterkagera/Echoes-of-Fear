using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SonarBatteryGuide : MonoBehaviour
{
    [Header("Input & Detection")]
    public KeyCode sonarKey = KeyCode.Q;
    public float maxSonarRange = 120f;
    public float cooldownTime = 3.0f;

    [Header("UI Directional Arrow (Canvas)")]
    public RectTransform arrowUI; // UI Image Arrow on HUD
    public float arrowDisplayDuration = 2.0f;

    [Header("Audio Ping Feedback")]
    public AudioSource audioSource;
    public AudioClip sonarPingSFX;
    public float minPingPitch = 0.8f;
    public float maxPingPitch = 1.8f;

    private float nextSonarTime = 0f;
    private Coroutine arrowRoutine;

    void Update()
    {
        if (Input.GetKeyDown(sonarKey) && Time.time >= nextSonarTime)
        {
            TriggerSonarPulse();
            nextSonarTime = Time.time + cooldownTime;
        }

        if (arrowUI != null && arrowUI.gameObject.activeInHierarchy)
        {
            UpdateArrowRotation();
        }
    }

    public void TriggerSonarPulse()
    {
        if (BatterySpawner.Instance == null) return;

        Transform closestBattery = BatterySpawner.Instance.GetClosestBattery(transform.position, maxSonarRange);

        if (closestBattery != null)
        {
            float distance = Vector3.Distance(transform.position, closestBattery.position);

            // Play Sonar Ping with higher pitch when closer to battery
            if (audioSource != null && sonarPingSFX != null)
            {
                float proximityPercent = 1f - Mathf.Clamp01(distance / maxSonarRange);
                audioSource.pitch = Mathf.Lerp(minPingPitch, maxPingPitch, proximityPercent);
                audioSource.PlayOneShot(sonarPingSFX);
            }

            // Show HUD Arrow pointing toward battery
            if (arrowUI != null)
            {
                if (arrowRoutine != null) StopCoroutine(arrowRoutine);
                arrowRoutine = StartCoroutine(ShowArrowRoutine(closestBattery));
            }
        }
    }

    private void UpdateArrowRotation()
    {
        if (BatterySpawner.Instance == null) return;
        Transform closestBattery = BatterySpawner.Instance.GetClosestBattery(transform.position, maxSonarRange);

        if (closestBattery == null) return;

        Vector3 dirToTarget = (closestBattery.position - transform.position).normalized;
        Vector3 forward = transform.forward;

        dirToTarget.y = 0;
        forward.y = 0;

        float angle = Vector3.SignedAngle(forward, dirToTarget, Vector3.up);
        arrowUI.localRotation = Quaternion.Euler(0, 0, -angle);
    }

    private IEnumerator ShowArrowRoutine(Transform targetBattery)
    {
        arrowUI.gameObject.SetActive(true);
        Image img = arrowUI.GetComponent<Image>();
        if (img != null) img.color = new Color(img.color.r, img.color.g, img.color.b, 1f);

        yield return new WaitForSeconds(arrowDisplayDuration);

        if (img != null)
        {
            float fadeTime = 0.5f;
            float elapsed = 0f;
            Color initialColor = img.color;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                img.color = new Color(initialColor.r, initialColor.g, initialColor.b, Mathf.Lerp(1f, 0f, elapsed / fadeTime));
                yield return null;
            }
        }

        arrowUI.gameObject.SetActive(false);
    }
}