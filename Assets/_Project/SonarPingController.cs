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
    public float maxLightIntensity = 6f;
    public float fadeOutDuration = 1.2f; // Time fog takes to slowly return to dark

    private MaterialPropertyBlock propBlock;
    private float currentRadius = 0f;
    private bool isPinging = false;
    private bool isFadingOut = false;
    private float fadeTimer = 0f;

    private static readonly int PulseRadiusID = Shader.PropertyToID("_PulseRadius");
    private static readonly int PulseCenterID = Shader.PropertyToID("_PulseCenter");

    void Awake()
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

        // Active Pulse Phase
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

                // Disable the wave mesh ring, but keep the light fading out
                if (sonarRenderer != null) sonarRenderer.enabled = false;
            }
        }

        // Dissolve Tail (Fog slowly reverts to original state)
        if (isFadingOut)
        {
            fadeTimer -= Time.deltaTime;
            float fadeProgress = Mathf.Clamp01(fadeTimer / fadeOutDuration);

            if (sonarLight != null)
            {
                // Smoothly lower intensity to zero over fadeOutDuration
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

        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI enemy in enemies)
        {
            enemy.AlertToSound(pingOrigin, maxRadius);
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