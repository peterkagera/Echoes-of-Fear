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

    [Header("Battery Radar")]
    public LayerMask batteryLayer;

    private MaterialPropertyBlock propBlock;
    private float currentRadius = 0f;
    private bool isPinging = false;
    private bool isFadingOut = false;
    private float fadeTimer = 0f;
    private Vector3 activePingOrigin;

    private readonly Collider[] hitBuffer = new Collider[16];
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
            sonarLight.shadows = LightShadows.None; // Disabled dynamic shadows on ping light to save mobile GPU cycles
        }

        if (RetinalAfterBurn.Instance != null)
        {
            RetinalAfterBurn.Instance.CaptureAfterBurn();
        }

        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null) enemies[i].AlertToSound(activePingOrigin, maxRadius);
        }

        ScanForBatteries(activePingOrigin);
    }

    private void ScanForBatteries(Vector3 origin)
    {
        // Non-allocating sphere check
        int hitCount = Physics.OverlapSphereNonAlloc(origin, maxRadius, hitBuffer, batteryLayer);
        for (int i = 0; i < hitCount; i++)
        {
            if (hitBuffer[i] != null && hitBuffer[i].GetComponent<BatteryPickup>() != null)
            {
                // Process battery detection if needed
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