using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class EnemyAI : MonoBehaviour
{
    [Header("Detection Settings")]
    public Transform player;
    public float baseDetectRadius = 10f;
    public float moveSpeed = 3.5f;

    [Header("Flashlight Detection")]
    public Light flashlight;
    public float lightBeamRange = 40f;
    public float ambientLightRange = 25f;

    [Header("Attack Settings")]
    public float killDistance = 2.2f;
    private bool isAttacking = false;

    private NavMeshAgent agent;
    private float pathUpdateTimer = 0f;
    private const float PATH_UPDATE_INTERVAL = 0.2f;

    void Awake()
    {
        // Auto-find Player by tag if not assigned
        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        // Auto-find Flashlight under Player if not assigned
        if (flashlight == null && player != null)
        {
            flashlight = player.GetComponentInChildren<Light>();
        }
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = 1.0f;
        }

        pathUpdateTimer = Random.Range(0f, PATH_UPDATE_INTERVAL);
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

        if (pathUpdateTimer >= PATH_UPDATE_INTERVAL)
        {
            pathUpdateTimer = 0f;

            if (flashlight != null && flashlight.enabled)
            {
                Vector3 directionToEnemy = (transform.position - player.position).normalized;
                float angle = Vector3.Angle(player.forward, directionToEnemy);

                if (distanceToPlayer <= lightBeamRange && angle < (flashlight.spotAngle / 2f))
                {
                    SetTargetPosition(player.position);
                    return;
                }

                if (distanceToPlayer <= ambientLightRange)
                {
                    SetTargetPosition(player.position);
                    return;
                }
            }

            if (distanceToPlayer <= baseDetectRadius)
            {
                SetTargetPosition(player.position);
            }
        }
    }

    public void AlertToSound(Vector3 soundOriginPosition)
    {
        if (!isAttacking)
        {
            SetTargetPosition(soundOriginPosition);
        }
    }

    private void SetTargetPosition(Vector3 targetPos)
    {
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    private void TriggerJumpscare()
    {
        isAttacking = true;
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
        }

        transform.LookAt(new Vector3(player.position.x, transform.position.y, player.position.z));

        Debug.Log("JUMPSCARE: The Stalker caught you!");

        Invoke(nameof(RestartLevel), 1.5f);
    }

    private void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}