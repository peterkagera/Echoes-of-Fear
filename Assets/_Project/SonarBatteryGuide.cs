using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Added for New Input System

public class SonarBatteryGuide : MonoBehaviour
{
    [Header("Input & Detection")]
    public float maxSonarRange = 120f;
    public float cooldownTime = 3.0f;

    [Header("UI Directional Arrow (Canvas)")]
    public RectTransform arrowUI;
    public float arrowDisplayDuration = 2.0f;

    [Header("Audio Ping Feedback")]
    public AudioSource audioSource;
    public AudioClip sonarPingSFX;
    public float minPingPitch = 0.8f;
    public float maxPingPitch = 1.8f;

    private float nextSonarTime = 0f;
    private Coroutine arrowRoutine;
    private Transform cachedClosestBattery;

    private void Update()
    {
        // PC Key check (Q key) using the new Input System
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            TryTriggerSonar();
        }

        if (arrowUI != null && arrowUI.gameObject.activeInHierarchy && cachedClosestBattery != null)
        {
            UpdateArrowRotation();
        }
    }

    // Call this from Update (PC) or a Mobile Canvas UI Button OnClick Event
    public void TryTriggerSonar()
    {
        if (Time.time >= nextSonarTime)
        {
            TriggerSonarPulse();
            nextSonarTime = Time.time + cooldownTime;
        }
    }

    public void TriggerSonarPulse()
    {
        if (BatterySpawner.Instance == null) return;

        cachedClosestBattery = BatterySpawner.Instance.GetClosestBattery(transform.position, maxSonarRange);
        if (cachedClosestBattery != null)
        {
            float distance = Vector3.Distance(transform.position, cachedClosestBattery.position);

            if (audioSource != null && sonarPingSFX != null)
            {
                float proximityPercent = 1f - Mathf.Clamp01(distance / maxSonarRange);
                audioSource.pitch = Mathf.Lerp(minPingPitch, maxPingPitch, proximityPercent);
                audioSource.PlayOneShot(sonarPingSFX);
            }

            if (arrowUI != null)
            {
                if (arrowRoutine != null) StopCoroutine(arrowRoutine);
                arrowRoutine = StartCoroutine(ShowArrowRoutine());
            }
        }
    }

    private void UpdateArrowRotation()
    {
        if (cachedClosestBattery == null) return;

        Vector3 dirToTarget = cachedClosestBattery.position - transform.position;
        dirToTarget.y = 0;
        Vector3 forward = transform.forward;
        forward.y = 0;

        float angle = Vector3.SignedAngle(forward, dirToTarget.normalized, Vector3.up);
        arrowUI.localRotation = Quaternion.Euler(0, 0, -angle);
    }

    private IEnumerator ShowArrowRoutine()
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
        cachedClosestBattery = null;
    }
}