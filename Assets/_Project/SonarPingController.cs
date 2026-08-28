using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SonarPingController : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform; // Drag Player here
    public Renderer sonarRenderer;
    public Light sonarLight;          // Drag your Light component here (optional)

    [Header("Sonar Settings")]
    public float maxRadius = 50f;
    public float pulseSpeed = 15f;
    public float maxLightIntensity = 5f; // Max light brightness during pulse

    private MaterialPropertyBlock propBlock;
    private float currentRadius = 0f;
    private bool isPinging = false;

    private static readonly int PulseRadiusID = Shader.PropertyToID("_PulseRadius");
    private static readonly int PulseCenterID = Shader.PropertyToID("_PulseCenter");

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();

        if (sonarRenderer == null)
        {
            sonarRenderer = GetComponent<Renderer>();
        }

        if (sonarLight == null)
        {
            sonarLight = GetComponent<Light>();
        }

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        // Diagnostic: Verify assigned components
        if (sonarRenderer == null)
        {
            Debug.LogError("[SonarPingController] Sonar Renderer is NULL! Assign a Renderer in the Inspector or attach this script to an object with a Renderer.", this);
        }
        else
        {
            Debug.Log($"[SonarPingController] Assigned Renderer: {sonarRenderer.gameObject.name}", sonarRenderer.gameObject);
        }
    }

    void Start()
    {
        ResetSonarMaterial();
    }

    void OnDisable()
    {
        ResetSonarMaterial();
    }

    void Update()
    {
        bool eKeyPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            eKeyPressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.E))
        {
            eKeyPressed = true;
        }
#endif

        if (eKeyPressed)
        {
            Debug.Log($"[SonarPingController] 'E' Key pressed. Current isPinging state: {isPinging}");
            if (!isPinging)
            {
                TriggerPing();
            }
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

            Debug.Log($"[SonarPingController] Pulse Expanding -> Current Radius: {currentRadius:F2} / {maxRadius}");

            // Expand light range and fade intensity out as ping travels
            if (sonarLight != null)
            {
                float progress = currentRadius / maxRadius;
                sonarLight.range = currentRadius;
                sonarLight.intensity = Mathf.Lerp(maxLightIntensity, 0f, progress);
            }

            if (currentRadius >= maxRadius)
            {
                Debug.Log("[SonarPingController] Ping reached max radius. Resetting.");
                isPinging = false;
                ResetSonarMaterial();
            }
        }
    }

    public void TriggerPing()
    {
        currentRadius = 0f;
        isPinging = true;

        Vector3 pingOrigin = (playerTransform != null) ? playerTransform.position : transform.position;
        Debug.Log($"[SonarPingController] Triggering Ping from Origin: {pingOrigin}");

        if (sonarLight != null)
        {
            sonarLight.transform.position = pingOrigin;
            sonarLight.enabled = true;
            sonarLight.range = 0f;
            sonarLight.intensity = maxLightIntensity;
        }

        if (sonarRenderer != null)
        {
            sonarRenderer.GetPropertyBlock(propBlock);
            propBlock.SetVector(PulseCenterID, pingOrigin);
            propBlock.SetFloat(PulseRadiusID, 0f);
            sonarRenderer.SetPropertyBlock(propBlock);
        }

        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        Debug.Log($"[SonarPingController] Notified {enemies.Length} enemy/enemies of the ping.");

        foreach (EnemyAI enemy in enemies)
        {
            enemy.AlertToSound(pingOrigin, maxRadius);
        }
    }

    private void ResetSonarMaterial()
    {
        if (sonarRenderer != null && propBlock != null)
        {
            sonarRenderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat(PulseRadiusID, 0f);
            sonarRenderer.SetPropertyBlock(propBlock);
        }

        if (sonarLight != null)
        {
            sonarLight.intensity = 0f;
            sonarLight.enabled = false;
        }
    }
}