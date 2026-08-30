using System.Collections;
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
    public float maxChaseDistance = 25f;

    [Header("Random Spawning")]
    public bool randomizeSpawnOnStart = false;
    public float spawnRadiusX = 25f;
    public float spawnRadiusZ = 25f;

    [Header("Speed & Variance")]
    public float minSpeed = 1.25f;
    public float maxSpeed = 1.9f;
    private float targetAssignedSpeed = 1.5f;

    [Header("Flashlight Detection")]
    public Light flashlight;
    public float lightBeamRange = 25f;

    [Header("Attack Settings")]
    public float killDistance = 1.8f;
    public float maxKillVerticalDistance = 2.0f;
    public float jumpscareDuration = 1.2f;
    public string jumpscareTriggerName = "Jumpscare";

    [Header("Smart Investigation")]
    public float investigateTime = 3f;
    private float searchTimer = 0f;

    [Header("Audio Settings")]
    public AudioSource approachAudioSource;
    public AudioClip footstepsSound;
    public AudioClip chaseSound;
    public AudioClip jumpscareSound;

    private NavMeshAgent agent;
    private Animator animator;
    private float pathUpdateTimer = 0f;
    private const float PATH_UPDATE_INTERVAL = 0.25f;
    private bool isAttacking = false;
    private Vector3 lastDestination;

    private float killDistanceSqr;
    private float maxChaseDistanceSqr;
    private float lightBeamRangeSqr;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
        {
            Debug.LogWarning($"[EnemyAI] Animator component not found in children of {gameObject.name}!");
        }

        if (agent == null)
        {
            Debug.LogError($"[EnemyAI] Critical Error: NavMeshAgent component missing from {gameObject.name}!");
        }

        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
            else
            {
                Debug.LogError($"[EnemyAI] Critical Error: Player reference could not be found or tagged in the scene for {gameObject.name}!");
            }
        }

        if (flashlight == null && player != null)
        {
            flashlight = player.GetComponentInChildren<Light>();
        }

        if (approachAudioSource == null)
        {
            approachAudioSource = GetComponent<AudioSource>();
            if (approachAudioSource == null)
            {
                approachAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        Configure3DAudio();

        if (animator != null)
        {
            animator.SetBool("isWalking", false);
        }
    }

    private void Configure3DAudio()
    {
        approachAudioSource.spatialBlend = 1.0f;
        approachAudioSource.rolloffMode = AudioRolloffMode.Linear;
        approachAudioSource.minDistance = 1.5f;
        approachAudioSource.maxDistance = 15.0f;
        approachAudioSource.loop = true;
        approachAudioSource.playOnAwake = false;
    }

    void Start()
    {
        killDistanceSqr = killDistance * killDistance;
        maxChaseDistanceSqr = maxChaseDistance * maxChaseDistance;
        lightBeamRangeSqr = lightBeamRange * lightBeamRange;

        if (randomizeSpawnOnStart)
        {
            RandomizeSpawnPosition();
        }

        if (agent != null)
        {
            targetAssignedSpeed = Random.Range(minSpeed, maxSpeed);
            agent.speed = targetAssignedSpeed;
            agent.acceleration = 3f;
            agent.stoppingDistance = 1.0f;
            agent.updateRotation = true;
            agent.updateUpAxis = true;
        }

        pathUpdateTimer = Random.Range(0f, PATH_UPDATE_INTERVAL);
        SetState(AIState.Dormant);
    }

    void Update()
    {
        if (player == null || agent == null || isAttacking) return;

        pathUpdateTimer += Time.deltaTime;
        if (pathUpdateTimer < PATH_UPDATE_INTERVAL) return;
        pathUpdateTimer = 0f;

        Vector3 playerPos = player.position;
        Vector3 transformPos = transform.position;

        Vector3 diff = transformPos - playerPos;
        float horizontalSqr = new Vector3(diff.x, 0f, diff.z).sqrMagnitude;
        float verticalDistance = Mathf.Abs(diff.y);

        if (currentState != AIState.Dormant && horizontalSqr <= killDistanceSqr && verticalDistance <= maxKillVerticalDistance)
        {
            StartCoroutine(JumpscareSequence());
            return;
        }

        float fullDistanceSqr = diff.sqrMagnitude;

        if (currentState != AIState.Dormant && flashlight != null && flashlight.enabled && fullDistanceSqr <= lightBeamRangeSqr)
        {
            Vector3 directionToEnemy = (transformPos - (playerPos + Vector3.up * 1.5f)).normalized;
            float angle = Vector3.Angle(player.forward, directionToEnemy);

            if (angle < (flashlight.spotAngle / 2f))
            {
                if (!Physics.Linecast(playerPos + Vector3.up * 1.5f, transformPos + Vector3.up * 1.5f, obstacleMask))
                {
                    SetState(AIState.ChasingPlayer);
                    SetTargetPosition(playerPos);
                    return;
                }
            }
        }

        switch (currentState)
        {
            case AIState.Dormant:
                break;

            case AIState.SearchingSound:
                if (HasLineOfSightToPlayer() && fullDistanceSqr <= maxChaseDistanceSqr)
                {
                    SetState(AIState.ChasingPlayer);
                    break;
                }

                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
                {
                    currentState = AIState.Investigating;
                    searchTimer = investigateTime;
                }
                break;

            case AIState.Investigating:
                if (HasLineOfSightToPlayer() && fullDistanceSqr <= maxChaseDistanceSqr)
                {
                    SetState(AIState.ChasingPlayer);
                    break;
                }

                searchTimer -= PATH_UPDATE_INTERVAL;
                if (searchTimer <= 0f)
                {
                    SetState(AIState.Dormant);
                }
                break;

            case AIState.ChasingPlayer:
                if (fullDistanceSqr <= maxChaseDistanceSqr && HasLineOfSightToPlayer())
                {
                    SetTargetPosition(playerPos);
                }
                else
                {
                    SetTargetPosition(playerPos);
                    currentState = AIState.SearchingSound;
                }
                break;
        }
    }

    public void AlertToSound(Vector3 soundOriginPosition, float soundRadius)
    {
        if (isAttacking) return;

        float soundRadiusSqr = soundRadius * soundRadius;
        float distanceToSoundSqr = (transform.position - soundOriginPosition).sqrMagnitude;

        if (distanceToSoundSqr <= soundRadiusSqr)
        {
            if (HasLineOfSightToPlayer())
            {
                SetState(AIState.ChasingPlayer);
                SetTargetPosition(player.position);
            }
            else
            {
                SetState(AIState.SearchingSound);
                SetTargetPosition(soundOriginPosition);
            }
        }
    }

    private bool HasLineOfSightToPlayer()
    {
        if (player == null) return false;
        Vector3 eyeLevelPos = transform.position + Vector3.up * 1.5f;
        Vector3 playerEyePos = player.position + Vector3.up * 1.5f;

        return !Physics.Linecast(eyeLevelPos, playerEyePos, obstacleMask);
    }

    private void RandomizeSpawnPosition()
    {
        if (agent == null) return;

        float minDistSqr = 225f;
        Vector3 candidatePosition = Vector3.zero;
        bool validSpotFound = false;

        for (int i = 0; i < 10; i++)
        {
            Vector3 randomPoint = new Vector3(
                Random.Range(-spawnRadiusX, spawnRadiusX),
                0f,
                Random.Range(-spawnRadiusZ, spawnRadiusZ)
            );

            if (player != null && (randomPoint - player.position).sqrMagnitude < minDistSqr)
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

    public void SetState(AIState newState)
    {
        currentState = newState;

        if (currentState == AIState.Dormant)
        {
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            if (animator != null) animator.SetBool("isWalking", false);
            StopApproachAudio();
        }
        else
        {
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.isStopped = false;
                agent.speed = targetAssignedSpeed * 0.5f;
                StartCoroutine(RampUpAgentSpeed());
            }
            if (animator != null) animator.SetBool("isWalking", true);
            PlayStateAudio(newState);
        }
    }

    private IEnumerator RampUpAgentSpeed()
    {
        float elapsed = 0f;
        float duration = 0.6f;
        while (elapsed < duration && agent != null && agent.isActiveAndEnabled)
        {
            elapsed += Time.deltaTime;
            agent.speed = Mathf.Lerp(targetAssignedSpeed * 0.5f, targetAssignedSpeed, elapsed / duration);
            yield return null;
        }
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.speed = targetAssignedSpeed;
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
        if (agent != null && agent.isActiveAndEnabled)
        {
            if ((targetPos - lastDestination).sqrMagnitude > 0.25f)
            {
                lastDestination = targetPos;
                agent.isStopped = false;
                agent.SetDestination(targetPos);
            }
        }
    }

    private IEnumerator JumpscareSequence()
    {
        isAttacking = true;

        if (transform.parent != null)
        {
            for (int i = 0; i < transform.parent.childCount; i++)
            {
                EnemyAI enemy = transform.parent.GetChild(i).GetComponent<EnemyAI>();
                if (enemy != null)
                {
                    enemy.isAttacking = true;
                    if (enemy.agent != null && enemy.agent.isActiveAndEnabled) enemy.agent.isStopped = true;
                    if (enemy.animator != null) enemy.animator.SetBool("isWalking", false);
                    if (enemy.approachAudioSource != null) enemy.approachAudioSource.Stop();
                    if (enemy != this) enemy.enabled = false;
                }
            }
        }

        // CRITICAL FIX: Explicitly disable the NavMeshAgent first so it stops overriding the enemy's world position
        if (agent != null)
        {
            agent.enabled = false;
        }

        if (player != null)
        {
            MonoBehaviour[] playerScripts = player.GetComponents<MonoBehaviour>();
            for (int i = 0; i < playerScripts.Length; i++)
            {
                MonoBehaviour s = playerScripts[i];
                if (s != this && !s.GetType().Name.Contains("Audio"))
                {
                    s.enabled = false;
                }
            }

            Camera mainCam = Camera.main;
            Transform camTransform = mainCam != null ? mainCam.transform : player;

            // Place enemy perfectly in front of the camera view
            Vector3 targetSpot = camTransform.position + camTransform.forward * 1.0f - Vector3.up * 0.2f;
            transform.position = targetSpot;

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int r = 0; r < renderers.Length; r++)
            {
                renderers[r].enabled = true;
            }

            transform.LookAt(new Vector3(camTransform.position.x, transform.position.y, camTransform.position.z));

            if (mainCam != null)
            {
                mainCam.transform.LookAt(transform.position + Vector3.up * 0.5f);
            }
        }

        if (animator != null)
        {
            animator.SetTrigger(jumpscareTriggerName);
        }

        if (jumpscareSound != null && approachAudioSource != null)
        {
            approachAudioSource.PlayOneShot(jumpscareSound);
        }

        yield return new WaitForSeconds(jumpscareDuration);

        GameOverManager manager = FindFirstObjectByType<GameOverManager>();
        if (manager != null)
        {
            manager.ShowGameOver();
        }
    }
}