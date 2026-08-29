using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public enum AIState { Dormant, SearchingSound, ChasingPlayer, Investigating }

    [Header("Current State")]
    [SerializeField] private AIState currentState = AIState.Dormant;

    [Header("Detection Settings")]
    public Transform player;
    public LayerMask obstacleMask;

    [Header("Random Spawning")]
    public bool randomizeSpawnOnStart = false;
    public float spawnRadiusX = 25f;
    public float spawnRadiusZ = 25f;

    [Header("Speed & Variance")]
    public float minSpeed = 2.5f;
    public float maxSpeed = 4.2f;

    [Header("Flashlight Detection")]
    public Light flashlight;
    public float lightBeamRange = 35f;

    [Header("Attack Settings")]
    public float killDistance = 2.2f;

    [Header("Smart Investigation")]
    public float investigateTime = 3f;
    private float searchTimer = 0f;

    [Header("Audio Settings")]
    public AudioSource approachAudioSource;
    public AudioClip footstepsSound;
    public AudioClip chaseSound;

    private NavMeshAgent agent;
    private Animator animator;
    private float pathUpdateTimer = 0f;
    private const float PATH_UPDATE_INTERVAL = 0.2f;
    private bool isAttacking = false;

    void Awake()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        if (flashlight == null && player != null)
        {
            flashlight = player.GetComponentInChildren<Light>();
        }

        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        if (approachAudioSource == null)
        {
            approachAudioSource = GetComponent<AudioSource>();
            if (approachAudioSource == null)
            {
                approachAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        Configure3DAudio();
    }

    private void Configure3DAudio()
    {
        approachAudioSource.spatialBlend = 1.0f;
        approachAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        approachAudioSource.minDistance = 2.0f;
        approachAudioSource.maxDistance = 35.0f;
        approachAudioSource.loop = true;
        approachAudioSource.playOnAwake = false;
    }

    void Start()
    {
        if (randomizeSpawnOnStart)
        {
            RandomizeSpawnPosition();
        }

        if (agent != null)
        {
            agent.speed = Random.Range(minSpeed, maxSpeed);
            agent.stoppingDistance = 1.0f;
            agent.updateRotation = true;
            agent.updateUpAxis = true;
        }

        pathUpdateTimer = Random.Range(0f, PATH_UPDATE_INTERVAL);

        // Explicitly set state to dormant to reset animations and agent stop states
        SetState(AIState.Dormant);
    }

    void Update()
    {
        if (player == null || agent == null || isAttacking) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= killDistance)
        {
            TriggerJumpscare();
            return;
        }

        pathUpdateTimer += Time.deltaTime;
        if (pathUpdateTimer < PATH_UPDATE_INTERVAL) return;
        pathUpdateTimer = 0f;

        if (flashlight != null && flashlight.enabled && distanceToPlayer <= lightBeamRange)
        {
            Vector3 directionToEnemy = (transform.position - (player.position + Vector3.up * 1.5f)).normalized;
            float angle = Vector3.Angle(player.forward, directionToEnemy);

            if (angle < (flashlight.spotAngle / 2f))
            {
                if (!Physics.Linecast(player.position + Vector3.up * 1.5f, transform.position + Vector3.up * 1.5f, obstacleMask))
                {
                    SetState(AIState.ChasingPlayer);
                    SetTargetPosition(player.position);
                    return;
                }
            }
        }

        switch (currentState)
        {
            case AIState.Dormant:
                break;

            case AIState.SearchingSound:
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
                {
                    currentState = AIState.Investigating;
                    searchTimer = investigateTime;
                }
                break;

            case AIState.Investigating:
                searchTimer -= PATH_UPDATE_INTERVAL;
                if (searchTimer <= 0f)
                {
                    SetState(AIState.Dormant);
                }
                break;

            case AIState.ChasingPlayer:
                if (HasLineOfSightToPlayer())
                {
                    SetTargetPosition(player.position);
                }
                else
                {
                    SetTargetPosition(player.position);
                    currentState = AIState.SearchingSound;
                }
                break;
        }
    }

    public void AlertToSound(Vector3 soundOriginPosition, float soundRadius)
    {
        if (isAttacking) return;

        float distanceToSound = Vector3.Distance(transform.position, soundOriginPosition);
        if (distanceToSound <= soundRadius)
        {
            SetState(AIState.SearchingSound);
            SetTargetPosition(soundOriginPosition);
        }
    }

    private bool HasLineOfSightToPlayer()
    {
        Vector3 eyeLevelPos = transform.position + Vector3.up * 1.5f;
        Vector3 playerEyePos = player.position + Vector3.up * 1.5f;

        return !Physics.Linecast(eyeLevelPos, playerEyePos, obstacleMask);
    }

    private void RandomizeSpawnPosition()
    {
        float minDistanceFromPlayer = 15f;
        Vector3 candidatePosition = Vector3.zero;
        bool validSpotFound = false;

        for (int i = 0; i < 10; i++)
        {
            Vector3 randomPoint = new Vector3(
                Random.Range(-spawnRadiusX, spawnRadiusX),
                0f,
                Random.Range(-spawnRadiusZ, spawnRadiusZ)
            );

            if (player != null && Vector3.Distance(randomPoint, player.position) < minDistanceFromPlayer)
            {
                continue;
            }

            candidatePosition = randomPoint;
            validSpotFound = true;
            break;
        }

        if (validSpotFound && NavMesh.SamplePosition(candidatePosition, out NavMeshHit hit, 15.0f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
    }

    private void SetState(AIState newState)
    {
        currentState = newState;

        if (currentState == AIState.Dormant)
        {
            agent.isStopped = true;
            if (animator != null) animator.SetBool("isWalking", false);
            StopApproachAudio();
        }
        else
        {
            agent.isStopped = false;
            if (animator != null) animator.SetBool("isWalking", true);
            PlayStateAudio(newState);
        }
    }

    private void PlayStateAudio(AIState state)
    {
        if (approachAudioSource == null) return;

        AudioClip targetClip = (state == AIState.ChasingPlayer && chaseSound != null) ? chaseSound : footstepsSound;

        if (targetClip != null)
        {
            if (approachAudioSource.clip != targetClip || !approachAudioSource.isPlaying)
            {
                approachAudioSource.clip = targetClip;
                approachAudioSource.playOnAwake = false;
                approachAudioSource.Play();
            }
        }
    }

    private void StopApproachAudio()
    {
        if (approachAudioSource != null && approachAudioSource.isPlaying)
        {
            approachAudioSource.Stop();
        }
    }

    private void SetTargetPosition(Vector3 targetPos)
    {
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
    }

    private void TriggerJumpscare()
    {
        isAttacking = true;

        EnemyAI[] allEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI enemy in allEnemies)
        {
            enemy.isAttacking = true;

            if (enemy.agent != null && enemy.agent.isActiveAndEnabled)
            {
                enemy.agent.isStopped = true;
            }

            if (enemy.animator != null)
            {
                enemy.animator.SetBool("isWalking", false);
            }

            if (enemy.approachAudioSource != null)
            {
                enemy.approachAudioSource.Stop();
                enemy.approachAudioSource.clip = null;
            }

            enemy.enabled = false;
        }

        transform.LookAt(new Vector3(player.position.x, transform.position.y, player.position.z));

        GameOverManager manager = FindFirstObjectByType<GameOverManager>();
        if (manager != null)
        {
            manager.ShowGameOver();
        }
    }
}