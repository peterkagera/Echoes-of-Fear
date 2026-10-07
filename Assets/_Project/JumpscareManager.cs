using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class JumpscareManager : MonoBehaviour
{
    public static JumpscareManager Instance { get; private set; }

    [Header("Camera & Target Settings")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float eyeLevelOffset = 1.6f;
    [SerializeField] private float enemyFaceDistance = 1.3f; // Ideal distance in front of camera
    [SerializeField] private float snapToFaceSpeed = 30f;
    [SerializeField] private float jumpscareDuration = 2.0f;

    [Header("Camera Shake & Zoom Settings")]
    [SerializeField] private float shakeIntensity = 0.08f;
    [SerializeField] private float shakeFrequency = 35f;
    [SerializeField] private float zoomFOV = 45f; // FOV punch for intense horror feel

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

        // Dim or disable player flashlight so the enemy texture isn't blown out white
        FlashlightController flashlight = FindFirstObjectByType<FlashlightController>();
        if (flashlight != null)
        {
            flashlight.enabled = false;
        }

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

        if (mainCamera != null)
        {
            MonoBehaviour[] camScripts = mainCamera.GetComponents<MonoBehaviour>();
            for (int i = 0; i < camScripts.Length; i++)
            {
                if (camScripts[i] != this) camScripts[i].enabled = false;
            }
        }

        if (attackingEnemy != null)
        {
            // FIX: Zero velocity BEFORE making the Rigidbody kinematic to prevent Unity 6 warning
            Rigidbody enemyRB = attackingEnemy.GetComponent<Rigidbody>();
            if (enemyRB != null)
            {
                if (!enemyRB.isKinematic)
                {
                    enemyRB.linearVelocity = Vector3.zero;
                    enemyRB.angularVelocity = Vector3.zero;
                }
                enemyRB.isKinematic = true;
            }

            NavMeshAgent attackingAgent = attackingEnemy.GetComponent<NavMeshAgent>();
            if (attackingAgent != null)
            {
                if (attackingAgent.enabled && attackingAgent.isOnNavMesh)
                {
                    attackingAgent.isStopped = true;
                    attackingAgent.velocity = Vector3.zero;
                }
                attackingAgent.enabled = false;
            }

            // Position monster directly in front of camera at face height facing the player
            if (mainCamera != null)
            {
                Vector3 camPos = mainCamera.transform.position;
                Vector3 camForward = mainCamera.transform.forward;
                camForward.y = 0f;
                camForward.Normalize();

                Vector3 targetMonsterPos = camPos + camForward * enemyFaceDistance;
                targetMonsterPos.y = camPos.y - eyeLevelOffset;
                attackingEnemy.position = targetMonsterPos;
                attackingEnemy.rotation = Quaternion.LookRotation(-camForward);
            }

            // Enable animation playback instead of freezing on frame 0
            Animator enemyAnim = attackingEnemy.GetComponentInChildren<Animator>();
            if (enemyAnim != null)
            {
                enemyAnim.enabled = true;
                enemyAnim.speed = 1.0f;
                enemyAnim.Play("Idle", 0, 0f);
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
        float originalFOV = mainCamera.fieldOfView;
        float elapsedTime = 0f;

        Vector3 faceTargetPos = enemyTransform.position + (Vector3.up * eyeLevelOffset);
        if (headTarget != null)
        {
            faceTargetPos = headTarget.position;
        }

        while (elapsedTime < jumpscareDuration)
        {
            // Snap camera directly to monster face
            Vector3 directionToFace = (faceTargetPos - mainCamera.transform.position).normalized;
            if (directionToFace != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToFace);
                mainCamera.transform.rotation = Quaternion.Slerp(
                    mainCamera.transform.rotation, targetRotation, Time.deltaTime * snapToFaceSpeed
                );
            }

            // Quick FOV zoom for cinematic impact
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, zoomFOV, Time.deltaTime * 12f);

            // Screen shake
            float shakeX = (Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) - 0.5f) * 2f * shakeIntensity;
            float shakeY = (Mathf.PerlinNoise(0f, Time.time * shakeFrequency) - 0.5f) * 2f * shakeIntensity;
            mainCamera.transform.localPosition = initialCamLocalPos + new Vector3(shakeX, shakeY, 0f);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.localPosition = initialCamLocalPos;
        mainCamera.fieldOfView = originalFOV;

        if (gameOverUI != null)
        {
            gameOverUI.SetActive(true);
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}