using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum AIState
    {
        Dormant,
        ChasingPlayer,
        SearchingSound,
        Patrolling
    }

    [Header("References")]
    public Transform player;
    public LayerMask obstacleMask;
    public Transform headTransform; // Drag the head bone here in the Inspector (optional)

    [Header("AI Settings")]
    public float moveSpeed = 3.5f;
    public float maxChaseDistance = 45f;
    public float killDistance = 1.8f;
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
    public bool enableDiagnostics = true;

    private NavMeshAgent agent;
    private Animator anim;
    private AIState currentState = AIState.Dormant;
    private float pathUpdateTimer = 0f;
    private const float PATH_UPDATE_INTERVAL = 0.25f;

    private Vector3 lastKnownPlayerPos;
    private float searchTimer = 0f;
    private bool isJumpscaring = false;
    private float logTimer = 0f;
    private float footstepTimer = 0f;

    // Desynchronization variables
    private float currentStepInterval;
    private float originalBasePitch = 1.0f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();

        if (anim != null)
        {
            anim.applyRootMotion = false;
        }

        // 1. RANDOMIZE SPEED: Vary movement speed slightly per enemy so their cadences naturally drift apart
        moveSpeed += Random.Range(-0.4f, 0.4f);

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.acceleration = 20f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.8f;
            agent.autoBraking = true;
        }

        // 2. RANDOMIZE STEP TIMERS: Give each enemy instance a unique initial footstep timer and interval jitter
        footstepTimer = Random.Range(0f, footstepInterval);
        currentStepInterval = footstepInterval + Random.Range(-0.05f, 0.05f);
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

        // 3. RANDOMIZE ANIMATION PHASE: Offset walking animation start frame
        if (anim != null)
        {
            anim.Play(0, -1, Random.Range(0f, 1f));
        }

        SetState(AIState.Dormant);
    }

    void Update()
    {
        if (player == null || isJumpscaring) return;

        bool isMoving = agent != null && agent.velocity.sqrMagnitude > 0.1f && !agent.isStopped;

        if (anim != null)
        {
            anim.SetBool("isWalking", isMoving);
        }

        // --- FOOTSTEP SOUND DESYNCHRONIZATION FIX ---
        if (isMoving && footstepSFX != null && footstepSFX.Length > 0)
        {
            footstepTimer += Time.deltaTime;
            if (footstepTimer >= currentStepInterval)
            {
                footstepTimer = 0f;

                // Vary step timing jitter per step so they don't stay locked in rhythm
                currentStepInterval = footstepInterval + Random.Range(-0.06f, 0.06f);

                AudioClip randomStep = footstepSFX[Random.Range(0, footstepSFX.Length)];
                if (audioSource != null && randomStep != null)
                {
                    // Randomize pitch and volume per footstep to make individuals sound unique
                    audioSource.pitch = originalBasePitch * Random.Range(0.88f, 1.12f);
                    float randomVolume = Random.Range(0.55f, 0.75f);
                    audioSource.PlayOneShot(randomStep, randomVolume);
                }
            }
        }
        else
        {
            // Instead of instant frame-0 triggers when starting to walk, set a randomized start delay
            footstepTimer = Random.Range(0f, footstepInterval * 0.6f);
        }

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        if (enableDiagnostics)
        {
            logTimer += Time.deltaTime;
            if (logTimer >= 2.0f)
            {
                logTimer = 0f;
                Debug.Log($"[{gameObject.name}] State={currentState} | Dist={distToPlayer:F1}m | Speed={agent.velocity.magnitude:F1}m/s | HasPath={agent.hasPath}", this);
            }
        }

        if (distToPlayer <= killDistance)
        {
            TriggerJumpscare();
            return;
        }

        switch (currentState)
        {
            case AIState.Dormant:
            case AIState.Patrolling:
                if (distToPlayer <= 4.0f || (distToPlayer <= detectionRange && HasLineOfSight()))
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

                if (distToPlayer > maxChaseDistance)
                {
                    lastKnownPlayerPos = player.position;
                    searchTimer = 0f;
                    SetState(AIState.SearchingSound);
                }
                break;

            case AIState.SearchingSound:
                if (distToPlayer <= detectionRange && HasLineOfSight())
                {
                    SetState(AIState.ChasingPlayer);
                    return;
                }

                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
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

        if (Physics.Raycast(eyePos, dir, out RaycastHit hit, detectionRange, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.transform.IsChildOf(player) || hit.transform == player;
        }
        return false;
    }

    public void AlertToSound(Vector3 soundPosition, float volume)
    {
        if (isJumpscaring || currentState == AIState.ChasingPlayer) return;

        float distToSound = Vector3.Distance(transform.position, soundPosition);
        if (distToSound <= detectionRange * volume)
        {
            SetState(AIState.SearchingSound);
            SetTargetPosition(soundPosition);
        }
    }

    public void SetTargetPosition(Vector3 targetPos)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(targetPos);
        }
    }

    public void SetState(AIState newState)
    {
        if (isJumpscaring || currentState == newState) return;

        if (enableDiagnostics)
        {
            Debug.Log($"[{gameObject.name}] State Change: {currentState} -> {newState}", this);
        }

        currentState = newState;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed;
        }
    }

    private void TriggerJumpscare()
    {
        if (isJumpscaring) return;
        isJumpscaring = true;

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        directionToPlayer.y = 0;
        transform.rotation = Quaternion.LookRotation(directionToPlayer);
        transform.position = player.position - (directionToPlayer * 0.6f);

        if (anim != null)
        {
            anim.SetBool("isWalking", false);
            anim.Play("Idle", 0, 0f);
            anim.speed = 0f;
        }

        if (agent != null)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();
            }
            agent.enabled = false;
        }

        if (JumpscareManager.Instance != null)
        {
            JumpscareManager.Instance.TriggerJumpscare(transform, headTransform);
        }

        this.enabled = false;
    }
}