using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SonarPingController : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform;
    public Renderer sonarRenderer;
    public Light sonarLight;

    [Header("Sonar Settings")]
    public float maxRadius = 50f;
    public float pulseSpeed = 20f;
    public float maxLightIntensity = 15f;
    public float fadeOutDuration = 1.2f;

    [Header("3D Light Expansion Settings")]
    public float lightHeightOffset = 4.0f;
    public float lightRangeMultiplier = 1.4f;

    [Header("Battery & Enemy Layers")]
    public LayerMask batteryLayer;
    public LayerMask enemyLayer;

    [Header("Defensive Shockwave Settings")]
    public float knockbackForce = 22f;
    public float stunDuration = 3.5f;

    private MaterialPropertyBlock propBlock;
    private float currentRadius = 0f;
    private bool isPinging = false;
    private bool isFadingOut = false;
    private float fadeTimer = 0f;
    private Vector3 activePingOrigin;

    private readonly Collider[] hitBuffer = new Collider[32];
    private readonly HashSet<EnemyAI> hitEnemiesThisPing = new HashSet<EnemyAI>();

    private static readonly int PulseRadiusID = Shader.PropertyToID("_PulseRadius");
    private static readonly int PulseCenterID = Shader.PropertyToID("_PulseCenter");

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        if (sonarRenderer == null) sonarRenderer = GetComponent<Renderer>();
        if (sonarLight == null) sonarLight = GetComponent<Light>();
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }
        ResetSonarMaterial();
    }

    private void Start() => ResetSonarMaterial();
    private void OnDisable() => ResetSonarMaterial();

    private void Update()
    {
        bool eKeyPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            eKeyPressed = true;
        }
#endif

        if (eKeyPressed && !isPinging && !isFadingOut)
        {
            TriggerPing();
        }

        if (isPinging)
        {
            currentRadius += pulseSpeed * Time.deltaTime;

            if (sonarRenderer != null)
            {
                sonarRenderer.GetPropertyBlock(propBlock);
                propBlock.SetFloat(PulseRadiusID, currentRadius);
                sonarRenderer.SetPropertyBlock(propBlock);
            }

            if (sonarLight != null)
            {
                sonarLight.transform.position = activePingOrigin + (Vector3.up * lightHeightOffset);
                float expanded3DRadius = Mathf.Sqrt((currentRadius * currentRadius) + (lightHeightOffset * lightHeightOffset));
                sonarLight.range = expanded3DRadius * lightRangeMultiplier;
                sonarLight.intensity = Mathf.Lerp(maxLightIntensity * 0.6f, maxLightIntensity, currentRadius / maxRadius);
            }

            // I evaluate shockwave hits dynamically as my visual ring expands outward
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
                sonarLight.intensity = Mathf.Lerp(0f, maxLightIntensity, fadeProgress);
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
        currentRadius = 0f;
        isPinging = true;
        isFadingOut = false;
        activePingOrigin = (playerTransform != null) ? playerTransform.position : transform.position;

        // I clear the tracking set at the start of each new pulse
        hitEnemiesThisPing.Clear();

        if (sonarRenderer != null)
        {
            sonarRenderer.enabled = true;
            sonarRenderer.GetPropertyBlock(propBlock);
            propBlock.SetVector(PulseCenterID, activePingOrigin);
            propBlock.SetFloat(PulseRadiusID, 0f);
            sonarRenderer.SetPropertyBlock(propBlock);
        }

        if (sonarLight != null)
        {
            sonarLight.transform.position = activePingOrigin + (Vector3.up * lightHeightOffset);
            sonarLight.enabled = true;
            sonarLight.range = lightHeightOffset * lightRangeMultiplier;
            sonarLight.intensity = maxLightIntensity * 0.6f;
            sonarLight.shadows = LightShadows.None;
        }

        if (RetinalAfterBurn.Instance != null)
        {
            RetinalAfterBurn.Instance.CaptureAfterBurn();
        }

        ScanForBatteries(activePingOrigin);
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
                // I register this enemy so they are only impacted once per wave
                hitEnemiesThisPing.Add(enemy);

                // I compute the directional force pushing them away from the center
                Vector3 knockbackDir = (enemy.transform.position - origin).normalized;
                knockbackDir.y = 0.25f; // I apply an upward arc to lift them off the ground

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
            if (propBlock != null)
            {
                sonarRenderer.GetPropertyBlock(propBlock);
                propBlock.SetFloat(PulseRadiusID, 0f);
                sonarRenderer.SetPropertyBlock(propBlock);
            }
            sonarRenderer.enabled = false;
        }

        if (sonarLight != null)
        {
            sonarLight.intensity = 0f;
            sonarLight.enabled = false;
        }
    }
}