using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum AIState { Dormant, ChasingPlayer, SearchingSound, Patrolling, Stunned }

    [Header("Combat Settings")]
    [SerializeField] private float attackDamage = 25f;

    [Header("References")]
    public Transform player;
    public LayerMask obstacleMask;
    public Transform headTransform;

    [Header("AI Settings")]
    public float moveSpeed = 3.5f;
    public float maxChaseDistance = 45f;
    public float killDistance = 2.8f;
    public float eyeLevelOffset = 1.6f;
    public float detectionRange = 12.0f;
    public float searchDuration = 5.0f;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip jumpscareSFX;
    public float jumpscareHoldDuration = 1.0f;
    public AudioClip[] footstepSFX;
    public float footstepInterval = 0.45f;

    [Header("Diagnostics")]
    public bool enableDiagnostics = false;

    private NavMeshAgent agent;
    private Animator anim;
    private Rigidbody rb;
    private AIState currentState = AIState.Dormant;
    private float pathUpdateTimer = 0f;
    private const float PATH_UPDATE_INTERVAL = 0.25f;
    private Vector3 lastKnownPlayerPos;
    private float searchTimer = 0f;
    private bool isJumpscaring = false;
    private bool isStunned = false;
    private bool isSpawning = true;
    private Coroutine stunRoutine;

    private float logTimer = 0f;
    private float footstepTimer = 0f;
    private float currentStepInterval;
    private float originalBasePitch = 1.0f;

    private float killDistanceSqr;
    private float detectionRangeSqr;
    private float maxChaseDistanceSqr;
    private const float NEARBY_DETECTION_SQR = 25.0f;

    private bool lastWalkingState = false;
    private int walkableAreaMask;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody>();

        if (anim != null)
        {
            anim.applyRootMotion = false;
        }

        walkableAreaMask = 1 << NavMesh.GetAreaFromName("Walkable");

        moveSpeed += Random.Range(-0.4f, 0.4f);
        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.acceleration = 30f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.1f;
            agent.autoBraking = false;
        }

        footstepTimer = Random.Range(0f, footstepInterval);
        currentStepInterval = footstepInterval + Random.Range(-0.05f, 0.05f);

        CacheSquaredDistances();
    }

    private void CacheSquaredDistances()
    {
        killDistanceSqr = killDistance * killDistance;
        detectionRangeSqr = detectionRange * detectionRange;
        maxChaseDistanceSqr = maxChaseDistance * maxChaseDistance;
    }

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource != null) originalBasePitch = audioSource.pitch;

        if (anim != null)
        {
            anim.Play(0, -1, Random.Range(0f, 1f));
        }

        StartCoroutine(SpawnDelayRoutine());
        SetState(AIState.Dormant);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") || (player != null && collision.transform.IsChildOf(player)))
        {
            AttackPlayer(collision.collider);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || (player != null && other.transform.IsChildOf(player)))
        {
            AttackPlayer(other);
        }
    }

    private void AttackPlayer(Collider playerCollider)
    {
        if (isJumpscaring || isStunned || isSpawning) return;

        Debug.Log("Enemy collided with: " + playerCollider.name);

        // Try to find PlayerHealth on the player or collider
        PlayerHealth playerHealth = playerCollider.GetComponent<PlayerHealth>();
        if (playerHealth == null && player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        if (playerHealth != null)
        {
            Debug.Log("PlayerHealth found! Dealing damage: " + attackDamage);
            playerHealth.TakeDamage(attackDamage);

            if (audioSource != null && jumpscareSFX != null)
            {
                audioSource.enabled = true;
                audioSource.PlayOneShot(jumpscareSFX);
            }

            if (playerHealth.CurrentHealth > 0f)
            {
                StartCoroutine(PostAttackCooldownRoutine());
                return;
            }
        }
        else
        {
            Debug.LogWarning("PlayerHealth component NOT found on player object!");
        }

        TriggerJumpscare();
    }

    private IEnumerator PostAttackCooldownRoutine()
    {
        isJumpscaring = true;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        yield return new WaitForSeconds(1.0f); // Short pause after landing a hit

        isJumpscaring = false;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }

    private IEnumerator SpawnDelayRoutine()
    {
        isSpawning = true;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (anim != null)
        {
            anim.SetBool("isWalking", false);
        }

        yield return new WaitForSeconds(2.5f);

        isSpawning = false;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }

    public void ResetSpawnDelay()
    {
        StartCoroutine(SpawnDelayRoutine());
    }

    void Update()
    {
        if (player == null || isJumpscaring || isStunned || isSpawning) return;

        bool isMoving = agent != null && agent.enabled && agent.velocity.sqrMagnitude > 0.1f && !agent.isStopped;

        if (anim != null && isMoving != lastWalkingState)
        {
            lastWalkingState = isMoving;
            anim.SetBool("isWalking", isMoving);
        }

        if (isMoving && footstepSFX != null && footstepSFX.Length > 0)
        {
            float currentSpeed = agent.velocity.magnitude;
            float dynamicInterval = footstepInterval / Mathf.Max(1f, currentSpeed / 3.5f);

            footstepTimer += Time.deltaTime;
            if (footstepTimer >= dynamicInterval)
            {
                footstepTimer = 0f;
                AudioClip randomStep = footstepSFX[Random.Range(0, footstepSFX.Length)];
                if (audioSource != null && randomStep != null && audioSource.enabled)
                {
                    audioSource.pitch = originalBasePitch * Random.Range(0.88f, 1.12f);
                    float randomVolume = Random.Range(0.55f, 0.75f);
                    audioSource.PlayOneShot(randomStep, randomVolume);
                }
            }
        }
        else
        {
            footstepTimer = Random.Range(0f, footstepInterval * 0.6f);
        }

        Vector3 enemyPos = transform.position;
        Vector3 playerPos = player.position;
        float dx = enemyPos.x - playerPos.x;
        float dz = enemyPos.z - playerPos.z;
        float sqrDistToPlayer = (dx * dx) + (dz * dz);

        if (sqrDistToPlayer <= killDistanceSqr)
        {
            AttackPlayer(player.GetComponent<Collider>() ?? player.GetComponentInChildren<Collider>());
            return;
        }

        switch (currentState)
        {
            case AIState.Dormant:
            case AIState.Patrolling:
                if (agent != null && agent.enabled && agent.isOnNavMesh && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance))
                {
                    searchTimer += Time.deltaTime;
                    if (searchTimer >= 3.5f)
                    {
                        searchTimer = 0f;
                        Vector3 randomDir = (Random.insideUnitSphere * 25f) + transform.position;
                        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, 12f, walkableAreaMask))
                        {
                            SetTargetPosition(hit.position);
                        }
                    }
                }

                if (sqrDistToPlayer <= NEARBY_DETECTION_SQR || (sqrDistToPlayer <= detectionRangeSqr && HasLineOfSight()))
                {
                    SetState(AIState.ChasingPlayer);
                }
                break;

            case AIState.ChasingPlayer:
                pathUpdateTimer += Time.deltaTime;
                if (pathUpdateTimer >= PATH_UPDATE_INTERVAL)
                {
                    pathUpdateTimer = 0f;
                    SetTargetPosition(player.position);
                }

                if (sqrDistToPlayer > maxChaseDistanceSqr)
                {
                    lastKnownPlayerPos = player.position;
                    searchTimer = 0f;
                    SetState(AIState.SearchingSound);
                }
                break;

            case AIState.SearchingSound:
                if (sqrDistToPlayer <= detectionRangeSqr && HasLineOfSight())
                {
                    SetState(AIState.ChasingPlayer);
                    return;
                }

                if (agent != null && agent.enabled && agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
                {
                    searchTimer += Time.deltaTime;
                    if (searchTimer >= searchDuration)
                    {
                        SetState(AIState.Dormant);
                    }
                }
                break;
        }
    }

    public bool HasLineOfSight()
    {
        if (player == null) return false;

        Vector3 eyePos = transform.position + (Vector3.up * eyeLevelOffset);
        Vector3 targetEyePos = player.position + (Vector3.up * eyeLevelOffset);
        Vector3 dir = (targetEyePos - eyePos).normalized;

        int mask = (obstacleMask.value != 0) ? obstacleMask.value : ~0;

        if (Physics.Raycast(eyePos, dir, out RaycastHit hit, detectionRange, mask, QueryTriggerInteraction.Ignore))
        {
            return hit.transform.IsChildOf(player) || hit.transform == player;
        }
        return false;
    }

    public void AlertToSound(Vector3 soundPosition, float volume)
    {
        if (isJumpscaring || isStunned || isSpawning || currentState == AIState.ChasingPlayer) return;

        float distToSound = Vector3.Distance(transform.position, soundPosition);
        if (distToSound <= detectionRange * volume)
        {
            SetState(AIState.SearchingSound);
            SetTargetPosition(soundPosition);
        }
    }

    public void ApplyKnockback(Vector3 force)
    {
        if (isJumpscaring || isSpawning) return;

        if (agent != null && agent.enabled)
        {
            agent.enabled = false;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce(force, ForceMode.Impulse);
        }
        else
        {
            transform.position += force * 0.05f;
        }
    }

    public void Stun(float duration)
    {
        if (isJumpscaring || isSpawning) return;

        if (stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
        }
        stunRoutine = StartCoroutine(StunRoutineProcess(duration));
    }

    private IEnumerator StunRoutineProcess(float duration)
    {
        isStunned = true;
        currentState = AIState.Stunned;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (anim != null)
        {
            anim.SetBool("isWalking", false);
            lastWalkingState = false;
        }

        yield return new WaitForSeconds(duration);

        if (rb != null)
        {
            rb.isKinematic = true;
        }

        isStunned = false;

        if (agent != null)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
            }
        }

        if (player != null && Vector3.SqrMagnitude(transform.position - player.position) <= detectionRangeSqr)
        {
            SetState(AIState.ChasingPlayer);
        }
        else
        {
            SetState(AIState.SearchingSound);
        }
    }

    public void SetTargetPosition(Vector3 targetPos)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh && !isSpawning)
        {
            agent.isStopped = false;
            agent.SetDestination(targetPos);
        }
    }

    public void SetState(AIState newState)
    {
        if (isJumpscaring || isStunned || isSpawning || currentState == newState) return;

        currentState = newState;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed;
        }
    }

    public void OnPlayerDeath()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
            audioSource.enabled = false;
        }

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (anim != null)
        {
            anim.SetBool("isWalking", false);
        }

        this.enabled = false;
    }

    private void TriggerJumpscare()
    {
        if (isJumpscaring && isStunned) return;
        isJumpscaring = true;

        if (audioSource != null && jumpscareSFX != null && !audioSource.isPlaying)
        {
            audioSource.enabled = true;
            audioSource.PlayOneShot(jumpscareSFX);
        }

        if (InceptionManager.Instance != null && InceptionManager.Instance.currentDreamLayer < InceptionManager.Instance.maxDreamLayers)
        {
            InceptionManager.Instance.TriggerDreamCollapse();

            // Reset player health on dream collapse so they get a fresh bar in the new layer
            PlayerHealth pHealth = player != null ? player.GetComponent<PlayerHealth>() : FindAnyObjectByType<PlayerHealth>();
            if (pHealth != null)
            {
                pHealth.Heal(pHealth.MaxHealth);
            }

            isJumpscaring = false;
            SetState(AIState.Dormant);
            StartCoroutine(SpawnDelayRoutine());
            return;
        }

        // Trigger the Game Over state, freeze time, and show the UI canvas
        GameOverManager gameOverManager = FindAnyObjectByType<GameOverManager>();
        if (gameOverManager != null)
        {
            gameOverManager.ShowGameOver();
        }

        EnemyAI[] allEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI enemy in allEnemies)
        {
            if (enemy != null)
            {
                enemy.OnPlayerDeath();
            }
        }

        if (player != null)
        {
            Vector3 directionToPlayer = (player.position - transform.position).normalized;
            directionToPlayer.y = 0;
            transform.rotation = Quaternion.LookRotation(directionToPlayer);
        }

        if (anim != null)
        {
            anim.SetBool("isWalking", false);
            anim.Play("Idle", 0, 0f);
            anim.speed = 0f;
        }

        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();
            }
            agent.enabled = false;
        }

        if (JumpscareManager.Instance != null && player != null)
        {
            JumpscareManager.Instance.TriggerJumpscare(transform, headTransform);
        }

        this.enabled = false;
    }
}