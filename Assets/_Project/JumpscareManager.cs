using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class JumpscareManager : MonoBehaviour
{
    public static JumpscareManager Instance { get; private set; }

    [Header("Camera & Target Settings")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float headHeightOffset = 1.6f;
    [SerializeField] private float snapToFaceSpeed = 25f;
    [SerializeField] private float jumpscareDuration = 2.0f;

    [Header("Camera Shake Settings")]
    [SerializeField] private float shakeIntensity = 0.1f;
    [SerializeField] private float shakeFrequency = 30f;

    [Header("Audio & UI")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip jumpscareSound;
    [SerializeField] private GameObject gameOverUI;

    [Header("Player References")]
    [SerializeField] private PlayerController playerController;

    private bool isJumpscareActive = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void TriggerJumpscare(Transform attackingEnemy, Transform headTarget = null)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopAmbience();
        }
        if (isJumpscareActive) return;
        isJumpscareActive = true;

        // 1. Disable player movement and look scripts completely
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        CharacterController playerCC = FindFirstObjectByType<CharacterController>();
        if (playerCC != null) playerCC.enabled = false;

        // 2. Freeze attacking enemy physics & NavMeshAgent
        NavMeshAgent attackingAgent = attackingEnemy.GetComponent<NavMeshAgent>();
        if (attackingAgent != null)
        {
            if (attackingAgent.isOnNavMesh)
            {
                attackingAgent.isStopped = true;
                attackingAgent.velocity = Vector3.zero;
            }
            attackingAgent.enabled = false;
        }

        Rigidbody enemyRB = attackingEnemy.GetComponent<Rigidbody>();
        if (enemyRB != null)
        {
            enemyRB.isKinematic = true;
            enemyRB.linearVelocity = Vector3.zero;
        }

        // 3. Freeze animator speed so movement frames stop immediately
        Animator enemyAnim = attackingEnemy.GetComponentInChildren<Animator>();
        if (enemyAnim != null)
        {
            enemyAnim.SetBool("isWalking", false);
            enemyAnim.Play("Idle", 0, 0f);
            enemyAnim.speed = 0f;
        }

        // 4. Deactivate all secondary enemies in the scene
        CleanUpOtherEnemies(attackingEnemy);

        // 5. Play audio
        if (audioSource != null && jumpscareSound != null)
        {
            audioSource.PlayOneShot(jumpscareSound);
        }

        // 6. Begin camera sequence
        StartCoroutine(JumpscareSequence(attackingEnemy, headTarget));
    }

    private void CleanUpOtherEnemies(Transform attackingEnemy)
    {
        NavMeshAgent[] allAgents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
        foreach (NavMeshAgent agent in allAgents)
        {
            if (agent.transform != attackingEnemy)
            {
                agent.isStopped = true;
                agent.gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator JumpscareSequence(Transform enemyTransform, Transform headTarget)
    {
        Vector3 initialCamLocalPos = mainCamera.transform.localPosition;
        float elapsedTime = 0f;

        // Auto-detect Head bone, ignoring IK bones near floor level
        if (headTarget == null)
        {
            Transform[] children = enemyTransform.GetComponentsInChildren<Transform>();
            foreach (Transform child in children)
            {
                string boneName = child.name.ToLower();
                if ((boneName.Contains("head") || boneName.Contains("neck")) &&
                    child.position.y > (enemyTransform.position.y + 0.8f))
                {
                    headTarget = child;
                    break;
                }
            }
        }

        while (elapsedTime < jumpscareDuration)
        {
            Vector3 targetHeadPos = (headTarget != null)
                ? headTarget.position
                : enemyTransform.position + (Vector3.up * headHeightOffset);

            Vector3 directionToHead = (targetHeadPos - mainCamera.transform.position).normalized;
            if (directionToHead != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToHead);
                mainCamera.transform.rotation = Quaternion.Slerp(
                    mainCamera.transform.rotation,
                    targetRotation,
                    Time.deltaTime * snapToFaceSpeed
                );
            }

            float shakeX = (Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) - 0.5f) * 2f * shakeIntensity;
            float shakeY = (Mathf.PerlinNoise(0f, Time.time * shakeFrequency) - 0.5f) * 2f * shakeIntensity;
            mainCamera.transform.localPosition = initialCamLocalPos + new Vector3(shakeX, shakeY, 0f);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.localPosition = initialCamLocalPos;

        if (gameOverUI != null)
        {
            gameOverUI.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}