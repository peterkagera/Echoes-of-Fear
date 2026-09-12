using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioSource ambientSource;

    [Header("Action Clips")]
    [SerializeField] private AudioClip flashlightToggleSFX;
    [SerializeField] private AudioClip batteryPickupSFX;
    [SerializeField] private AudioClip[] footstepSFXArray;

    [Header("Ambient Horror SFX")]
    [SerializeField] private AudioClip[] ambientHorrorClips;
    [Tooltip("Base timer range between ambient sounds (in seconds).")]
    [SerializeField] private Vector2 baseIntervalRange = new Vector2(5f, 12f);
    [Tooltip("Minimum timer range when enemy is close.")]
    [SerializeField] private Vector2 panicIntervalRange = new Vector2(2f, 5f);
    [Tooltip("Distance from enemy where tension maxes out.")]
    [SerializeField] private float panicDistanceThreshold = 15f;

    [Header("3D Spatialization Settings")]
    [SerializeField] private bool useDirectionalSpawns = true;
    [SerializeField] private float minSpawnDistance = 6f;
    [SerializeField] private float maxSpawnDistance = 14f;

    [Header("Diagnostics")]
    [SerializeField] private bool enableDiagnostics = false;

    private float ambientTimer;
    private bool isPlayerAlive = true;
    private Transform playerTransform;
    private Transform mainCameraTransform;

    // Cache enemy array to avoid scene-wide allocations every step
    private EnemyAI[] cachedEnemies;
    private float enemyCacheTimer = 0f;
    private const float ENEMY_CACHE_INTERVAL = 1.5f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }

        ResetAmbientTimer();
    }

    private void Update()
    {
        if (!isPlayerAlive) return;
        HandleAmbientHorror();
    }

    public void PlayFlashlightToggle() => PlaySFX(flashlightToggleSFX);
    public void PlayBatteryPickup() => PlaySFX(batteryPickupSFX);

    public void PlayFootstep()
    {
        if (footstepSFXArray == null || footstepSFXArray.Length == 0 || footstepSource == null) return;

        AudioClip clip = footstepSFXArray[Random.Range(0, footstepSFXArray.Length)];
        footstepSource.pitch = Random.Range(0.85f, 1.15f);
        footstepSource.PlayOneShot(clip);

        if (playerTransform != null)
        {
            EnemyAI[] enemies = GetCachedEnemies();
            float noiseRadius = 18f;
            foreach (EnemyAI enemy in enemies)
            {
                if (enemy != null && enemy.enabled)
                {
                    enemy.AlertToSound(playerTransform.position, noiseRadius);
                }
            }
        }
    }

    public void StopAmbience()
    {
        isPlayerAlive = false;
        if (ambientSource != null)
        {
            ambientSource.Stop();
        }
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    private void HandleAmbientHorror()
    {
        if (ambientHorrorClips == null || ambientHorrorClips.Length == 0) return;

        ambientTimer -= Time.deltaTime;
        if (ambientTimer <= 0f)
        {
            PlayDirectionalAmbient();
            ResetAmbientTimer();
        }
    }

    private void PlayDirectionalAmbient()
    {
        if (ambientSource == null || ambientHorrorClips.Length == 0) return;

        AudioClip clip = ambientHorrorClips[Random.Range(0, ambientHorrorClips.Length)];
        ambientSource.pitch = Random.Range(0.88f, 1.12f);

        if (useDirectionalSpawns && playerTransform != null && mainCameraTransform != null)
        {
            Vector3 randomDirection = -mainCameraTransform.forward + (Random.insideUnitSphere * 0.8f);
            randomDirection.y = 0;
            randomDirection.Normalize();

            float spawnDistance = Random.Range(minSpawnDistance, maxSpawnDistance);
            Vector3 soundPosition = playerTransform.position + (randomDirection * spawnDistance);

            ambientSource.transform.position = soundPosition;
        }

        ambientSource.PlayOneShot(clip);
    }

    private void ResetAmbientTimer()
    {
        Vector2 activeRange = baseIntervalRange;

        if (playerTransform != null)
        {
            EnemyAI[] enemies = GetCachedEnemies();
            float minSqrDist = panicDistanceThreshold * panicDistanceThreshold;
            bool foundCloseEnemy = false;

            foreach (EnemyAI enemy in enemies)
            {
                if (enemy != null && enemy.enabled)
                {
                    float sqrDist = (playerTransform.position - enemy.transform.position).sqrMagnitude;
                    if (sqrDist < minSqrDist)
                    {
                        minSqrDist = sqrDist;
                        foundCloseEnemy = true;
                    }
                }
            }

            if (foundCloseEnemy)
            {
                float distToEnemy = Mathf.Sqrt(minSqrDist);
                float tensionFactor = Mathf.Clamp01(distToEnemy / panicDistanceThreshold);
                activeRange.x = Mathf.Lerp(panicIntervalRange.x, baseIntervalRange.x, tensionFactor);
                activeRange.y = Mathf.Lerp(panicIntervalRange.y, baseIntervalRange.y, tensionFactor);
            }
        }

        ambientTimer = Random.Range(activeRange.x, activeRange.y);
    }

    private EnemyAI[] GetCachedEnemies()
    {
        if (cachedEnemies == null || Time.time - enemyCacheTimer > ENEMY_CACHE_INTERVAL)
        {
            cachedEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            enemyCacheTimer = Time.time;
        }
        return cachedEnemies;
    }
}