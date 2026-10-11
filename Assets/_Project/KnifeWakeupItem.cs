using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class KnifeWakeupItem : MonoBehaviour
{
    [Header("References")]
    public Animator armsAnimator;
    public string triggerName = "CutHand";
    public AudioSource audioSource;

    [Header("Multi-Slash Audio Pool")]
    public AudioClip[] slashSounds;

    [Header("Multi-Hit Timing (Seconds from Start)")]
    public float[] cutTimestamps = new float[] { 1.1f, 1.9f, 2.7f, 3.5f };
    public float totalSequenceDuration = 4.5f;

    [Header("Violent Camera Warp Settings")]
    public Camera mainCamera;
    public float shakeDuration = 0.8f;
    public float shakeIntensity = 0.1f;
    public float fovKickAmount = 5f;

    [Header("Combat & Blood Settings")]
    public Transform attackPoint; // Point at the tip of the knife blade
    public float attackRange = 1.8f;
    public float attackDamage = 35f;
    public LayerMask enemyLayer; // Set this to your Enemy layer

    // Public static flag so Sonar knows not to interrupt the knife sequence
    public static bool IsExecuting = false;

    private Renderer[] childRenderers;
    private int currentSlashIndex = 0;

    void Awake()
    {
        if (armsAnimator == null) armsAnimator = GetComponent<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (mainCamera == null) mainCamera = Camera.main;

        childRenderers = GetComponentsInChildren<Renderer>(true);
        SetMeshesVisible(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame && !IsExecuting)
        {
            AttemptSelfHarmWakeUp();
        }
    }

    public void AttemptSelfHarmWakeUp()
    {
        if (IsExecuting) return;
        StartCoroutine(ExecuteWakeUpSequence());
    }

    private IEnumerator ExecuteWakeUpSequence()
    {
        IsExecuting = true;
        currentSlashIndex = 0;

        SetMeshesVisible(true);
        if (armsAnimator != null)
        {
            armsAnimator.SetTrigger(triggerName);
        }

        if (InceptionManager.Instance != null)
        {
            InceptionManager.Instance.WakeUpFromDream();
        }

        float startTime = Time.time;

        for (int i = 0; i < cutTimestamps.Length; i++)
        {
            float targetTime = startTime + cutTimestamps[i];
            while (Time.time < targetTime)
            {
                yield return null;
            }

            PlayNextSlashSound();

            if (mainCamera != null)
            {
                StartCoroutine(WarpCameraRoutine());
            }

            // DEAL DAMAGE & TRIGGER BLOOD SPLATTER ON EACH SLASH TIMESTAMP
            PerformSlashDamage();
        }

        float endTime = startTime + totalSequenceDuration;
        while (Time.time < endTime)
        {
            yield return null;
        }

        SetMeshesVisible(false);
        IsExecuting = false;
    }

    private void PerformSlashDamage()
    {
        if (attackPoint == null) attackPoint = transform;

        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange, enemyLayer);

        foreach (Collider enemyCol in hitEnemies)
        {
            if (enemyCol == null) continue;

            EnemyHealth enemyHealth = enemyCol.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null)
            {
                Vector3 hitPoint = enemyCol.ClosestPoint(attackPoint.position);
                Vector3 hitNormal = (attackPoint.position - hitPoint).normalized;

                // Safeguard against zero vector errors
                if (hitNormal == Vector3.zero)
                {
                    hitNormal = (mainCamera.transform.position - hitPoint).normalized;
                }

                enemyHealth.TakeDamage(attackDamage, hitPoint, hitNormal);
            }
        }
    }

    private void PlayNextSlashSound()
    {
        if (audioSource != null && slashSounds != null && slashSounds.Length > 0)
        {
            AudioClip clip = slashSounds[currentSlashIndex % slashSounds.Length];
            audioSource.PlayOneShot(clip);
            currentSlashIndex++;
        }
    }

    private void SetMeshesVisible(bool visible)
    {
        if (childRenderers == null) return;
        foreach (Renderer r in childRenderers)
        {
            if (r != null)
            {
                r.enabled = visible;
            }
        }
    }

    private IEnumerator WarpCameraRoutine()
    {
        Vector3 origPos = mainCamera.transform.localPosition;
        Quaternion origRot = mainCamera.transform.localRotation;
        float origFOV = mainCamera.fieldOfView;

        float timer = 0f;
        while (timer < shakeDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / shakeDuration;

            float x = Random.Range(-1f, 1f) * shakeIntensity * (1f - progress);
            float y = Random.Range(-1f, 1f) * shakeIntensity * (1f - progress);
            mainCamera.transform.localPosition = origPos + new Vector3(x, y, 0f);

            float rotZ = Random.Range(-2f, 2f) * (1f - progress);
            mainCamera.transform.localRotation = origRot * Quaternion.Euler(0f, 0f, rotZ);

            mainCamera.fieldOfView = Mathf.Lerp(origFOV + fovKickAmount, origFOV, progress);

            yield return null;
        }

        mainCamera.transform.localPosition = origPos;
        mainCamera.transform.localRotation = origRot;
        mainCamera.fieldOfView = origFOV;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
    }
}