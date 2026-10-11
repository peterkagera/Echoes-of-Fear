using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Impact VFX")]
    [Tooltip("Assign a prefab from Assets/Vefects/Free Blood VFX/VFX/Particles/Once")]
    public GameObject bloodVfxPrefab;

    [Header("Death Settings")]
    public Animator animator;
    public Component enemyAiScript; // Optional AI script to disable on death

    private bool isDead = false;

    private void Awake()
    {
        currentHealth = maxHealth;
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (isDead) return;

        currentHealth -= amount;

        // 1. Spawn 3D Blood Particle Burst aligned with hit angle
        if (bloodVfxPrefab != null)
        {
            Quaternion bloodRotation = Quaternion.LookRotation(hitNormal);
            GameObject bloodInstance = Instantiate(bloodVfxPrefab, hitPoint, bloodRotation);
            Destroy(bloodInstance, 3.0f); // Auto-cleanup particle
        }

        // 2. Trigger Camera Lens Blood Splash
        if (CameraBloodSplatter.Instance != null)
        {
            CameraBloodSplatter.Instance.TriggerSplatter();
        }

        // 3. Handle Death
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;

        // Trigger Death Animation or Ragdoll
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        // Disable AI behavior script on death
        if (enemyAiScript != null)
        {
            if (enemyAiScript is Behaviour b) b.enabled = false;
        }

        // Disable colliders so player can walk through dead body
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Destroy(gameObject, 5.0f); // Clean up corpse after 5 seconds
    }
}