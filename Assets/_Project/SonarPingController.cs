using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SonarPingController : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform; // Drag Player here
    public Renderer sonarRenderer;

    [Header("Sonar Settings")]
    public float maxRadius = 50f;
    public float pulseSpeed = 15f;

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

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
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

        if (eKeyPressed && !isPinging)
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

            if (currentRadius >= maxRadius)
            {
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

        if (sonarRenderer != null)
        {
            sonarRenderer.GetPropertyBlock(propBlock);
            propBlock.SetVector(PulseCenterID, pingOrigin);
            propBlock.SetFloat(PulseRadiusID, 0f);
            sonarRenderer.SetPropertyBlock(propBlock);
        }

        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI enemy in enemies)
        {
            enemy.AlertToSound(pingOrigin);
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
    }
}