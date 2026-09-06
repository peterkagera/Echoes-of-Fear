using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum AIState { Dormant, ChasingPlayer, SearchingSound, Patrolling }

    [Header("References")]
    public Transform player;
    public LayerMask obstacleMask;
    public Transform headTransform;

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

        moveSpeed += Random.Range(-0.4f, 0.4f);
        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.acceleration = 20f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.8f;
            agent.autoBraking = true;
        }

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

        if (isMoving && footstepSFX != null && footstepSFX.Length > 0)
        {
            footstepTimer += Time.deltaTime;
            if (footstepTimer >= currentStepInterval)
            {
                footstepTimer = 0f;
                currentStepInterval = footstepInterval + Random.Range(-0.06f, 0.06f);
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

        Vector3 enemyPosXZ = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 playerPosXZ = new Vector3(player.position.x, 0f, player.position.z);
        float distToPlayer = Vector3.Distance(enemyPosXZ, playerPosXZ);

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
                if (agent != null && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance))
                {
                    searchTimer += Time.deltaTime;
                    if (searchTimer >= 3.5f)
                    {
                        searchTimer = 0f;
                        Vector3 randomDir = (Random.insideUnitSphere * 25f) + transform.position;
                        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, 12f, NavMesh.AllAreas))
                        {
                            SetTargetPosition(hit.position);
                        }
                    }
                }

                if (distToPlayer <= 5.0f || (distToPlayer <= detectionRange && HasLineOfSight()))
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

                if (agent != null && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
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
        if (isJumpscaring) return;
        isJumpscaring = true;

        // Stop audio & AI on ALL active enemies in scene upon player death
        EnemyAI[] allEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI enemy in allEnemies)
        {
            if (enemy != null)
            {
                enemy.OnPlayerDeath();
            }
        }

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        directionToPlayer.y = 0;
        transform.rotation = Quaternion.LookRotation(directionToPlayer);

        Vector3 targetKillPos = player.position - (directionToPlayer * killDistance);

        if (Physics.Raycast(targetKillPos + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            targetKillPos.y = groundHit.point.y;
        }

        transform.position = targetKillPos;

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