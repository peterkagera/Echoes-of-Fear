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
    public Camera mainCamera;

    [Header("Sonar Settings")]
    public float maxRadius = 50f;
    public float pulseSpeed = 20f;
    [Tooltip("High intensity for a powerful illuminating pulse wave.")]
    public float maxLightIntensity = 30f;
    public float fadeOutDuration = 1.2f;

    [Header("3D Light Expansion & Canopy Settings")]
    [Tooltip("Height offset of the light above the player ground position.")]
    public float lightHeightOffset = 4.0f;
    [Tooltip("Multiplier to push light boundaries beyond the ground ring edge so high/distant geometry gets hit.")]
    public float lightRangeMultiplier = 1.4f;

    [Header("Battery Radar")]
    public LayerMask batteryLayer;
    public Texture2D batteryBlipIcon;
    public Color blipColor = Color.cyan;
    public float blipDisplayDuration = 2.0f;

    private MaterialPropertyBlock propBlock;
    private float currentRadius = 0f;
    private bool isPinging = false;
    private bool isFadingOut = false;
    private float fadeTimer = 0f;
    private Vector3 activePingOrigin;

    private List<Vector3> detectedBatteries = new List<Vector3>();
    private float blipTimer = 0f;

    private static readonly int PulseRadiusID = Shader.PropertyToID("_PulseRadius");
    private static readonly int PulseCenterID = Shader.PropertyToID("_PulseCenter");

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();

        if (sonarRenderer == null) sonarRenderer = GetComponent<Renderer>();
        if (sonarLight == null) sonarLight = GetComponent<Light>();
        if (mainCamera == null) mainCamera = Camera.main;

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        ResetSonarMaterial();
    }

    void Start() => ResetSonarMaterial();
    void OnDisable() => ResetSonarMaterial();

    void Update()
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

            // 1. Update Shader Material (Ground Ring)
            if (sonarRenderer != null)
            {
                sonarRenderer.GetPropertyBlock(propBlock);
                propBlock.SetFloat(PulseRadiusID, currentRadius);
                sonarRenderer.SetPropertyBlock(propBlock);
            }

            // 2. Dynamic 3D Spherical Light Expansion
            if (sonarLight != null)
            {
                sonarLight.transform.position = activePingOrigin + (Vector3.up * lightHeightOffset);

                // Calculate true 3D hypotenuse radius to sync horizontal expansion with vertical tree coverage
                float expanded3DRadius = Mathf.Sqrt((currentRadius * currentRadius) + (lightHeightOffset * lightHeightOffset));
                sonarLight.range = expanded3DRadius * lightRangeMultiplier;

                // Scale light intensity dynamically as radius expands so far away objects pop brightly
                float radiusRatio = Mathf.Clamp01(currentRadius / maxRadius);
                sonarLight.intensity = Mathf.Lerp(maxLightIntensity * 0.6f, maxLightIntensity, radiusRatio);
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

        if (blipTimer > 0f)
        {
            blipTimer -= Time.deltaTime;
            if (blipTimer <= 0f)
            {
                detectedBatteries.Clear();
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
        }

        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI enemy in enemies)
        {
            enemy.AlertToSound(activePingOrigin, maxRadius);
        }

        ScanForBatteries(activePingOrigin);
    }

    private void ScanForBatteries(Vector3 origin)
    {
        detectedBatteries.Clear();
        Collider[] hits = Physics.OverlapSphere(origin, maxRadius, batteryLayer);

        foreach (Collider hit in hits)
        {
            if (hit.GetComponent<BatteryPickup>() != null || hit.CompareTag("Battery"))
            {
                detectedBatteries.Add(hit.transform.position);
            }
        }

        if (detectedBatteries.Count > 0)
        {
            blipTimer = blipDisplayDuration;
        }
    }

    private void OnGUI()
    {
        if (blipTimer <= 0f || mainCamera == null || detectedBatteries.Count == 0) return;

        Color originalColor = GUI.color;
        GUI.color = new Color(blipColor.r, blipColor.g, blipColor.b, blipTimer / blipDisplayDuration);

        foreach (Vector3 worldPos in detectedBatteries)
        {
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            if (screenPos.z > 0)
            {
                float guiY = Screen.height - screenPos.y;
                float size = 16f;
                Rect rect = new Rect(screenPos.x - size / 2f, guiY - size / 2f, size, size);

                if (batteryBlipIcon != null)
                {
                    GUI.DrawTexture(rect, batteryBlipIcon);
                }
                else
                {
                    GUI.Box(rect, "⚡");
                }
            }
        }

        GUI.color = originalColor;
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