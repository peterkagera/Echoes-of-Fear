using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public enum AIState { Dormant, SearchingSound, ChasingPlayer, Investigating }

    [Header("Current State")]
    [SerializeField] private AIState currentState = AIState.Dormant;

    [Header("Detection Settings")]
    public Transform player;
    public LayerMask obstacleMask; // Set this to Default / Everything including Trees

    [Header("Random Spawning")]
    public bool randomizeSpawnOnStart = true;
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
    }

    void Start()
    {
        // 1. Randomize spawn position across terrain navmesh on game start
        if (randomizeSpawnOnStart)
        {
            RandomizeSpawnPosition();
        }

        // 2. Assign unique movement parameters
        if (agent != null)
        {
            agent.speed = Random.Range(minSpeed, maxSpeed);
            agent.stoppingDistance = 1.0f;
            agent.isStopped = true; // Complete freeze on start
        }

        pathUpdateTimer = Random.Range(0f, PATH_UPDATE_INTERVAL);
    }

    void Update()
    {
        if (player == null || agent == null || isAttacking) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Kill check
        if (distanceToPlayer <= killDistance)
        {
            TriggerJumpscare();
            return;
        }

        // Stagger frame updates
        pathUpdateTimer += Time.deltaTime;
        if (pathUpdateTimer < PATH_UPDATE_INTERVAL) return;
        pathUpdateTimer = 0f;

        // Flashlight Light-Beam Detection (with Raycast Line-of-Sight Check)
        if (flashlight != null && flashlight.enabled && distanceToPlayer <= lightBeamRange)
        {
            Vector3 directionToEnemy = (transform.position - (player.position + Vector3.up * 1.5f)).normalized;
            float angle = Vector3.Angle(player.forward, directionToEnemy);

            if (angle < (flashlight.spotAngle / 2f))
            {
                // Raycast to ensure no tree trunk blocks the light ray
                if (!Physics.Linecast(player.position + Vector3.up * 1.5f, transform.position + Vector3.up * 1.5f, obstacleMask))
                {
                    SetState(AIState.ChasingPlayer);
                    SetTargetPosition(player.position);
                    return;
                }
            }
        }

        // Behavior State Machine Execution
        switch (currentState)
        {
            case AIState.Dormant:
                // Completely stationary until triggered by noise or direct flashlight contact
                break;

            case AIState.SearchingSound:
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
                {
                    // Reached sound origin; search surrounding area briefly before returning dormant
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
                // Keep updating path toward moving player
                if (HasLineOfSightToPlayer())
                {
                    SetTargetPosition(player.position);
                }
                else
                {
                    // Lost line of sight; walk to player's last seen position then investigate
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
        float minDistanceFromPlayer = 15f; // Keeps enemies at a safe distance on start
        Vector3 candidatePosition = Vector3.zero;
        bool validSpotFound = false;

        for (int i = 0; i < 10; i++) // Try up to 10 times to find a far point
        {
            Vector3 randomPoint = new Vector3(
                Random.Range(-spawnRadiusX, spawnRadiusX),
                0f,
                Random.Range(-spawnRadiusZ, spawnRadiusZ)
            );

            if (player != null && Vector3.Distance(randomPoint, player.position) < minDistanceFromPlayer)
            {
                continue; // Too close to player, try again
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
        }
        else
        {
            agent.isStopped = false;
            if (animator != null) animator.SetBool("isWalking", true);
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
        if (agent != null && agent.isActiveAndEnabled) agent.isStopped = true;

        transform.LookAt(new Vector3(player.position.x, transform.position.y, player.position.z));

        GameOverManager manager = FindFirstObjectByType<GameOverManager>();
        if (manager != null) manager.ShowGameOver();
    }
}