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
    public float footstepInterval = 0.45f; // Time between steps while chasing

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

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();

        if (anim != null)
        {
            anim.applyRootMotion = false;
        }

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.acceleration = 20f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.8f;
            agent.autoBraking = true;
        }
    }

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

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

        // --- Footstep Audio Playback ---
        if (isMoving && footstepSFX != null && footstepSFX.Length > 0)
        {
            footstepTimer += Time.deltaTime;
            if (footstepTimer >= footstepInterval)
            {
                footstepTimer = 0f;
                AudioClip randomStep = footstepSFX[Random.Range(0, footstepSFX.Length)];
                if (audioSource != null && randomStep != null)
                {
                    audioSource.PlayOneShot(randomStep, 0.7f);
                }
            }
        }
        else
        {
            footstepTimer = footstepInterval; // Reset timer so step plays immediately when starting movement
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
        StartCoroutine(JumpscareRoutine());
    }

    private IEnumerator JumpscareRoutine()
    {
        isJumpscaring = true;

        if (agent != null)
        {
            agent.enabled = false;
        }

        MonoBehaviour playerCtrl = player.GetComponent("PlayerController") as MonoBehaviour;
        if (playerCtrl != null) playerCtrl.enabled = false;

        Camera mainCam = Camera.main;
        Transform viewTransform = mainCam != null ? mainCam.transform : player;

        Vector3 camForward = viewTransform.forward;
        camForward.y = 0f;
        camForward.Normalize();

        transform.position = viewTransform.position + (camForward * 1.3f) - (Vector3.up * 0.4f);
        transform.rotation = Quaternion.LookRotation(-camForward);

        if (mainCam != null)
        {
            Vector3 enemyHead = transform.position + (Vector3.up * eyeLevelOffset);
            mainCam.transform.LookAt(enemyHead);
        }

        if (audioSource != null && jumpscareSFX != null)
        {
            audioSource.PlayOneShot(jumpscareSFX);
        }

        yield return new WaitForSeconds(jumpscareHoldDuration);

        GameOverManager gameOverManager = FindFirstObjectByType<GameOverManager>();
        if (gameOverManager != null)
        {
            gameOverManager.ShowGameOver();
        }
    }
}