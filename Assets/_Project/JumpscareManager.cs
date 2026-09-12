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
    [SerializeField] private float shakeIntensity = 0.06f;
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

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    public void TriggerJumpscare(Transform attackingEnemy, Transform headTarget = null)
    {
        if (isJumpscareActive) return;
        isJumpscareActive = true;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopAmbience();
        }

        TukTukVehicle vehicle = FindFirstObjectByType<TukTukVehicle>();
        if (vehicle != null)
        {
            vehicle.OnPlayerKilled();
        }

        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        if (mainCamera != null)
        {
            MonoBehaviour[] camScripts = mainCamera.GetComponents<MonoBehaviour>();
            for (int i = 0; i < camScripts.Length; i++)
            {
                if (camScripts[i] != this) camScripts[i].enabled = false;
            }
        }

        CharacterController playerCC = FindFirstObjectByType<CharacterController>();
        if (playerCC != null) playerCC.enabled = false;

        if (attackingEnemy != null)
        {
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

            Animator enemyAnim = attackingEnemy.GetComponentInChildren<Animator>();
            if (enemyAnim != null)
            {
                enemyAnim.SetBool("isWalking", false);
                enemyAnim.Play("Idle", 0, 0f);
                enemyAnim.speed = 0f;
            }

            CleanUpOtherEnemies(attackingEnemy);
        }

        if (audioSource != null && jumpscareSound != null)
        {
            audioSource.PlayOneShot(jumpscareSound);
        }

        StartCoroutine(JumpscareSequence(attackingEnemy, headTarget));
    }

    private void CleanUpOtherEnemies(Transform attackingEnemy)
    {
        EnemyAI[] allEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        for (int i = 0; i < allEnemies.Length; i++)
        {
            if (allEnemies[i] != null && allEnemies[i].transform != attackingEnemy)
            {
                allEnemies[i].gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator JumpscareSequence(Transform enemyTransform, Transform headTarget)
    {
        if (mainCamera == null || enemyTransform == null) yield break;

        Vector3 initialCamLocalPos = mainCamera.transform.localPosition;
        float elapsedTime = 0f;

        Vector3 faceTargetPos = enemyTransform.position + (Vector3.up * headHeightOffset);
        if (headTarget != null)
        {
            float relativeBoneHeight = headTarget.position.y - enemyTransform.position.y;
            if (relativeBoneHeight >= 1.0f && relativeBoneHeight <= 2.2f)
            {
                faceTargetPos = headTarget.position;
            }
        }

        while (elapsedTime < jumpscareDuration)
        {
            Vector3 directionToFace = (faceTargetPos - mainCamera.transform.position).normalized;
            if (directionToFace != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToFace);
                mainCamera.transform.rotation = Quaternion.Slerp(
                    mainCamera.transform.rotation, targetRotation, Time.deltaTime * snapToFaceSpeed
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