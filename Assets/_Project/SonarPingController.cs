using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SonarPingController : MonoBehaviour
{
    [Header("Sonar Visual & Magical Color Settings")]
    [Tooltip("Rich arcane violet/ethereal blue for a magical spell glow reflection")]
    public Color sonarColor = new Color(0.3f, 0.5f, 1.0f, 1f);
    public float adjustedMaxIntensity = 12.0f;

    [Header("Visual Effects & AfterBurn")]
    [Tooltip("Uncheck if the double vision / blur effect is too disorienting for players")]
    public bool enableAfterBurn = false; // Default set to false so it doesn't confuse players

    [Header("References")]
    public Transform playerTransform;
    public Renderer sonarRenderer;
    public Light sonarLight;

    [Header("Sonar Settings")]
    public float maxRadius = 50f;
    public float pulseSpeed = 20f;
    public float fadeOutDuration = 1.2f;

    [Header("3D Light Expansion Settings")]
    public float lightHeightOffset = 1.5f;
    public float lightRangeMultiplier = 1.4f;

    [Header("Camera Shake Settings (Rotation & FOV Punch)")]
    public float shakeDuration = 0.3f;
    public float shakeIntensity = 2.5f; // Rotation roll magnitude in degrees

    [Header("Battery & Enemy Layers")]
    public LayerMask batteryLayer;
    public LayerMask enemyLayer;

    [Header("Defensive Shockwave Settings")]
    public float knockbackForce = 22f;
    public float stunDuration = 3.5f;

    [Header("Defensive Left Arm Animation (L_wrist_Goal)")]
    public Transform lWristGoal;
    public Vector3 pushPositionOffset = new Vector3(0.15f, 0.35f, 0.4f);
    public float pushDuration = 0.45f;

    private float currentRadius = 0f;
    private bool isPinging = false;
    private bool isFadingOut = false;
    private float fadeTimer = 0f;
    private Vector3 activePingOrigin;

    private Vector3 originalWristPos;
    private Coroutine wristAnimRoutine;
    private Coroutine shakeRoutine;
    private Renderer[] armRenderers;
    private List<Renderer> excludedRenderers = new List<Renderer>();

    private readonly Collider[] hitBuffer = new Collider[32];
    private readonly HashSet<EnemyAI> hitEnemiesThisPing = new HashSet<EnemyAI>();

    private void Awake()
    {
        if (sonarRenderer == null) sonarRenderer = GetComponent<Renderer>();
        if (sonarLight == null) sonarLight = GetComponent<Light>();
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        if (lWristGoal == null)
        {
            GameObject wristObj = GameObject.Find("L_wrist_Goal");
            if (wristObj != null) lWristGoal = wristObj.transform;
        }

        if (lWristGoal != null)
        {
            originalWristPos = lWristGoal.localPosition;
        }

        GameObject armsObj = GameObject.Find("arms@knife");
        if (armsObj != null)
        {
            armRenderers = armsObj.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in armRenderers)
            {
                string objName = r.gameObject.name.ToLower();
                if (objName.Contains("knife") || objName.Contains("blade") || objName.Contains("right") || objName.Contains("r_"))
                {
                    excludedRenderers.Add(r);
                }
            }
        }

        ResetSonarMaterial();
    }

    private void Start() => ResetSonarMaterial();
    private void OnDisable() => ResetSonarMaterial();

    private void Update()
    {
        if (KnifeWakeupItem.IsExecuting) return;

        // REMOVED the hardcoded E key check here so collecting batteries doesn't trigger sonar.
        // Sonar is now safely controlled entirely via code / TriggerPing().

        if (isPinging)
        {
            currentRadius += pulseSpeed * Time.deltaTime;

            if (sonarLight != null)
            {
                sonarLight.color = sonarColor;
                float progress = Mathf.Clamp01(currentRadius / maxRadius);
                float dynamicHeight = Mathf.Lerp(0.2f, lightHeightOffset, progress);
                sonarLight.transform.position = activePingOrigin + (Vector3.up * dynamicHeight);

                float expanded3DRadius = Mathf.Sqrt((currentRadius * currentRadius) + (dynamicHeight * dynamicHeight));
                sonarLight.range = Mathf.Max(25f, expanded3DRadius * lightRangeMultiplier);
                sonarLight.intensity = Mathf.Lerp(adjustedMaxIntensity * 0.7f, adjustedMaxIntensity, progress);
            }

            ApplyWavefrontShockwave(activePingOrigin, currentRadius);

            if (currentRadius >= maxRadius)
            {
                isPinging = false;
                isFadingOut = true;
                fadeTimer = fadeOutDuration;
                if (sonarRenderer != null) sonarRenderer.enabled = false;
            }
        }

        if (isFadingOut)
        {
            fadeTimer -= Time.deltaTime;
            float fadeProgress = Mathf.Clamp01(fadeTimer / fadeOutDuration);
            if (sonarLight != null)
            {
                sonarLight.color = sonarColor;
                sonarLight.intensity = Mathf.Lerp(0f, adjustedMaxIntensity, fadeProgress);
            }

            if (fadeTimer <= 0f)
            {
                isFadingOut = false;
                ResetSonarMaterial();
            }
        }
    }

    public void TriggerPing()
    {
        if (KnifeWakeupItem.IsExecuting) return;

        currentRadius = 0f;
        isPinging = true;
        isFadingOut = false;
        activePingOrigin = (playerTransform != null) ? playerTransform.position : transform.position;

        hitEnemiesThisPing.Clear();

        ApplyInstantShockwave(activePingOrigin, 6.0f);

        if (sonarRenderer != null)
        {
            sonarRenderer.enabled = false;
        }

        if (sonarLight != null)
        {
            sonarLight.color = sonarColor;
            sonarLight.transform.position = activePingOrigin + (Vector3.up * 0.2f);
            sonarLight.enabled = true;
            sonarLight.range = 25f;
            sonarLight.intensity = adjustedMaxIntensity * 0.7f;
            sonarLight.shadows = LightShadows.Soft;
        }

        // Only trigger afterburn if explicitly enabled in inspector
        if (enableAfterBurn && RetinalAfterBurn.Instance != null)
        {
            RetinalAfterBurn.Instance.CaptureAfterBurn();
        }

        // Trigger visceral rotation roll shake that won't be overridden by player mouse-look scripts
        if (Camera.main != null)
        {
            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(CameraShakeRotationRoutine(Camera.main.transform));
        }

        if (lWristGoal != null)
        {
            if (wristAnimRoutine != null) StopCoroutine(wristAnimRoutine);
            wristAnimRoutine = StartCoroutine(AnimateWristPushRoutine());
        }

        ScanForBatteries(activePingOrigin);
    }

    private IEnumerator CameraShakeRotationRoutine(Transform camTransform)
    {
        Quaternion originalRot = camTransform.localRotation;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / shakeDuration;
            float currentIntensity = Mathf.Lerp(shakeIntensity, 0f, percent);

            float randomZRoll = Random.Range(-currentIntensity, currentIntensity);
            camTransform.localRotation = originalRot * Quaternion.Euler(0f, 0f, randomZRoll);

            yield return null;
        }

        camTransform.localRotation = originalRot;
    }

    private void ApplyInstantShockwave(Vector3 origin, float instantRadius)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(origin, instantRadius, hitBuffer, enemyLayer);
        for (int i = 0; i < hitCount; i++)
        {
            if (hitBuffer[i] == null) continue;

            EnemyAI enemy = hitBuffer[i].GetComponentInParent<EnemyAI>();
            if (enemy != null && !hitEnemiesThisPing.Contains(enemy))
            {
                hitEnemiesThisPing.Add(enemy);

                Vector3 knockbackDir = (enemy.transform.position - origin).normalized;
                knockbackDir.y = 0.25f;

                enemy.ApplyKnockback(knockbackDir * knockbackForce);
                enemy.Stun(stunDuration);
            }
        }
    }

    private IEnumerator AnimateWristPushRoutine()
    {
        SetArmsVisible(true);

        float timer = 0f;
        float snapTime = pushDuration * 0.35f;
        float returnTime = pushDuration * 0.65f;
        Vector3 targetPos = originalWristPos + pushPositionOffset;

        while (timer < snapTime)
        {
            timer += Time.deltaTime;
            float progress = timer / snapTime;
            lWristGoal.localPosition = Vector3.Lerp(originalWristPos, targetPos, Mathf.SmoothStep(0f, 1f, progress));
            yield return null;
        }

        timer = 0f;
        while (timer < returnTime)
        {
            timer += Time.deltaTime;
            float progress = timer / returnTime;
            lWristGoal.localPosition = Vector3.Lerp(targetPos, originalWristPos, Mathf.SmoothStep(0f, 1f, progress));
            yield return null;
        }

        lWristGoal.localPosition = originalWristPos;
        SetArmsVisible(false);
    }

    private void SetArmsVisible(bool visible)
    {
        if (armRenderers == null) return;
        foreach (Renderer r in armRenderers)
        {
            if (r != null)
            {
                if (excludedRenderers.Contains(r))
                {
                    r.enabled = false;
                }
                else
                {
                    r.enabled = visible;
                }
            }
        }
    }

    private void ApplyWavefrontShockwave(Vector3 origin, float radius)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(origin, radius, hitBuffer, enemyLayer);
        for (int i = 0; i < hitCount; i++)
        {
            if (hitBuffer[i] == null) continue;

            EnemyAI enemy = hitBuffer[i].GetComponentInParent<EnemyAI>();
            if (enemy != null && !hitEnemiesThisPing.Contains(enemy))
            {
                hitEnemiesThisPing.Add(enemy);

                Vector3 knockbackDir = (enemy.transform.position - origin).normalized;
                knockbackDir.y = 0.25f;

                enemy.ApplyKnockback(knockbackDir * knockbackForce);
                enemy.Stun(stunDuration);
            }
        }
    }

    private void ScanForBatteries(Vector3 origin)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(origin, maxRadius, hitBuffer, batteryLayer);
        for (int i = 0; i < hitCount; i++)
        {
            if (hitBuffer[i] != null && hitBuffer[i].GetComponent<BatteryPickup>() != null)
            {
                // Process battery detection
            }
        }
    }

    private void ResetSonarMaterial()
    {
        if (sonarRenderer != null)
        {
            sonarRenderer.enabled = false;
        }

        if (sonarLight != null)
        {
            sonarLight.color = sonarColor;
            sonarLight.intensity = 0f;
            sonarLight.enabled = false;
        }
    }
}