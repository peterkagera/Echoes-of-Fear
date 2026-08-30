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
    public float maxLightIntensity = 6f;
    public float fadeOutDuration = 1.2f;

    [Header("Battery Radar (Option B)")]
    public LayerMask batteryLayer;          // Set to Interactable or Default
    public Texture2D batteryBlipIcon;       // Optional custom UI dot/icon texture
    public Color blipColor = Color.cyan;
    public float blipDisplayDuration = 2.0f;

    private MaterialPropertyBlock propBlock;
    private float currentRadius = 0f;
    private bool isPinging = false;
    private bool isFadingOut = false;
    private float fadeTimer = 0f;

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

            if (sonarRenderer != null)
            {
                sonarRenderer.GetPropertyBlock(propBlock);
                propBlock.SetFloat(PulseRadiusID, currentRadius);
                sonarRenderer.SetPropertyBlock(propBlock);
            }

            if (sonarLight != null)
            {
                sonarLight.range = currentRadius;
                sonarLight.intensity = maxLightIntensity;
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

        Vector3 pingOrigin = (playerTransform != null) ? playerTransform.position : transform.position;

        if (sonarRenderer != null)
        {
            sonarRenderer.enabled = true;
            sonarRenderer.GetPropertyBlock(propBlock);
            propBlock.SetVector(PulseCenterID, pingOrigin);
            propBlock.SetFloat(PulseRadiusID, 0f);
            sonarRenderer.SetPropertyBlock(propBlock);
        }

        if (sonarLight != null)
        {
            sonarLight.transform.position = pingOrigin;
            sonarLight.enabled = true;
            sonarLight.range = 0f;
            sonarLight.intensity = maxLightIntensity;
        }

        // Alert Enemies
        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI enemy in enemies)
        {
            enemy.AlertToSound(pingOrigin, maxRadius);
        }

        // Detect Batteries within range
        ScanForBatteries(pingOrigin);
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

            // Ensure battery is in front of the camera frustum
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
                    // Fallback visual box marker
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