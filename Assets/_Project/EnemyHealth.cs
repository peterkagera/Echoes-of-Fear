using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    [SerializeField] private float currentHealth;

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

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (isDead) return;

        currentHealth -= amount;
        Debug.Log(gameObject.name + " took damage: " + amount + " | Current Health: " + currentHealth);

        // 1. Spawn 3D Blood Particle Burst aligned with hit angle
        if (bloodVfxPrefab != null)
        {
            Quaternion bloodRotation = Quaternion.LookRotation(hitNormal);
            GameObject bloodInstance = Instantiate(bloodVfxPrefab, hitPoint, bloodRotation);
            Destroy(bloodInstance, 3.0f);
        }

        // 2. Trigger Camera Lens Blood Splash
        if (CameraBloodSplatter.Instance != null)
        {
            CameraBloodSplatter.Instance.TriggerSplatter();
        }

        // 3. Handle Death
        if (currentHealth <= 0f)
        {
            Die(hitNormal);
        }
    }

    private void Die(Vector3 hitNormal)
    {
        if (isDead) return;
        isDead = true;

        // 1. Detach controller and disable animator to stop walking loop
        if (animator != null)
        {
            animator.runtimeAnimatorController = null;
            animator.enabled = false;
        }

        // 2. Disable AI behavior script so it stops tracking the player
        if (enemyAiScript != null)
        {
            if (enemyAiScript is Behaviour b) b.enabled = false;
        }

        // 3. Disable NavMeshAgent so pathfinding stops overriding physics
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }

        // 4. Ensure Rigidbody exists and configure physical collapse
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            // Unlock all rotation constraints so the body can topple over
            rb.constraints = RigidbodyConstraints.None;

            // Balanced mass and drag for a realistic animal weight collapse
            rb.mass = 40f;
            rb.linearDamping = 2.0f;
            rb.angularDamping = 2.0f;

            // Push backward and sharply downward to break its stance
            Vector3 pushDir = -hitNormal.normalized;
            pushDir.y = -1.0f;
            rb.AddForce(pushDir * 180f, ForceMode.Impulse);

            // Apply a heavy sideways rotation torque so it flips onto its side naturally
            Vector3 sideFlipTorque = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(1f, 2f)).normalized * 60f;
            rb.AddTorque(sideFlipTorque, ForceMode.Impulse);
        }

        // 5. Ensure Collider exists and is solid
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
        }

        if (col != null)
        {
            col.enabled = true;
            col.isTrigger = false;
        }

        // Clean up corpse after 5 seconds
        Destroy(gameObject, 5.0f);
    }
}