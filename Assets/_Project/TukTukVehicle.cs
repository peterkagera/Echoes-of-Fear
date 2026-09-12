using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TukTukVehicle : MonoBehaviour, IInteractable
{
    [Header("Seat Anchors")]
    public Transform driverSeat;
    public Transform exitPoint;

    [Header("Wheel Colliders")]
    public WheelCollider frontWheel;
    public WheelCollider backWheelLeft;
    public WheelCollider backWheelRight;

    [Header("Vehicle Physics")]
    public float motorTorque = 9000f;
    public float brakeTorque = 25000f;
    public float idleBrakeTorque = 8000f;
    public float maxSteerAngle = 42f;
    public float minSteerAngleAtSpeed = 12f;
    public float steerSpeed = 120f;
    public Vector3 centerOfMassOffset = new Vector3(0f, -1.2f, 0f);

    [Header("Stability & Anti-Roll Settings")]
    public float antiRollForce = 12000f;
    public float downforce = 150f;

    [Header("Path & Headlight Vision")]
    public Light headlight;
    public Light pathLight;
    public Light cabLight;
    public AudioSource audioSource;

    [Header("Engine Noise Detection")]
    public float engineNoiseRadius = 60f;
    public float noiseBroadcastInterval = 0.8f;
    public LayerMask enemyLayer;
    private float noiseTimer = 0f;

    [Header("Tree Battering Ram")]
    public float minRamSpeed = 0.5f;
    public GameObject splinterPrefab;
    public AudioClip woodSnapSound;

    [Header("Optional Direct References")]
    [SerializeField] private SonarPingController sonarController;

    private bool isDriving = false;
    private float enterCooldown = 0f;
    private GameObject playerObj;
    private CharacterController playerController;
    private PlayerController playerMovementScript;
    private Collider playerCollider;
    private Rigidbody rb;

    private float currentSteerInput = 0f;
    private float currentAccelInput = 0f;

    private int treeLayerIndex;
    private readonly Collider[] enemyHitBuffer = new Collider[16];

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.centerOfMass = centerOfMassOffset;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearDamping = 0.15f;
            rb.angularDamping = 4.0f;
        }

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (sonarController == null) sonarController = FindAnyObjectByType<SonarPingController>();

        treeLayerIndex = LayerMask.NameToLayer("Tree");
        SetVehicleLights(false);
        CachePlayerReferences();
    }

    public string GetPrompt() => isDriving ? "" : "Drive Tuk-Tuk";

    public void Interact()
    {
        if (!isDriving && enterCooldown <= 0f) EnterVehicle();
    }

    private void Update()
    {
        if (enterCooldown > 0f) enterCooldown -= Time.deltaTime;

        if (!isDriving) return;

        noiseTimer += Time.deltaTime;
        if (noiseTimer >= noiseBroadcastInterval)
        {
            noiseTimer = 0f;
            AlertNearbyEnemies(transform.position, engineNoiseRadius);
        }

        currentSteerInput = 0f;
        currentAccelInput = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) currentAccelInput = 1f;
            if (Keyboard.current.sKey.isPressed) currentAccelInput = -1f;
            if (Keyboard.current.aKey.isPressed) currentSteerInput = -1f;
            if (Keyboard.current.dKey.isPressed) currentSteerInput = 1f;

            if (Keyboard.current.eKey.wasPressedThisFrame && enterCooldown <= 0f)
            {
                ExitVehicle();
                return;
            }

            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                TriggerSonarPulse();
            }
        }
    }

    private void FixedUpdate()
    {
        if (!isDriving || rb == null) return;

        float forwardSpeedMps = rb.linearVelocity.magnitude;
        float speedRatio = Mathf.Clamp01(forwardSpeedMps / 15f);
        float dynamicMaxSteer = Mathf.Lerp(maxSteerAngle, minSteerAngleAtSpeed, speedRatio);

        float targetSteerAngle = currentSteerInput * dynamicMaxSteer;

        if (frontWheel != null)
        {
            frontWheel.steerAngle = Mathf.MoveTowards(
                frontWheel.steerAngle,
                targetSteerAngle,
                steerSpeed * Time.fixedDeltaTime * 10f
            );
        }

        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        if (currentAccelInput > 0f)
        {
            if (forwardSpeed < -0.5f) ApplyBrakes(brakeTorque);
            else ApplyMotor(currentAccelInput * motorTorque);
        }
        else if (currentAccelInput < 0f)
        {
            if (forwardSpeed > 0.5f) ApplyBrakes(brakeTorque);
            else ApplyMotor(currentAccelInput * motorTorque);
        }
        else
        {
            ApplyBrakes(idleBrakeTorque);
        }

        ApplyAntiRollBar();
        ApplyDownforce();
    }

    public void OnPlayerKilled()
    {
        isDriving = false;
        currentAccelInput = 0f;
        currentSteerInput = 0f;

        ApplyMotor(0f);
        ApplyBrakes(brakeTorque);
        SetVehicleLights(false);

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    private void ApplyAntiRollBar()
    {
        if (backWheelLeft == null || backWheelRight == null) return;

        bool groundedLeft = backWheelLeft.GetGroundHit(out WheelHit hitLeft);
        bool groundedRight = backWheelRight.GetGroundHit(out WheelHit hitRight);

        float travelLeft = groundedLeft ? (-backWheelLeft.transform.InverseTransformPoint(hitLeft.point).y - backWheelLeft.radius) / backWheelLeft.suspensionDistance : 1.0f;
        float travelRight = groundedRight ? (-backWheelRight.transform.InverseTransformPoint(hitRight.point).y - backWheelRight.radius) / backWheelRight.suspensionDistance : 1.0f;

        float antiRollForceAmount = (travelLeft - travelRight) * antiRollForce;

        if (groundedLeft)
        {
            rb.AddForceAtPosition(transform.up * -antiRollForceAmount, backWheelLeft.transform.position);
        }

        if (groundedRight)
        {
            rb.AddForceAtPosition(transform.up * antiRollForceAmount, backWheelRight.transform.position);
        }
    }

    private void ApplyDownforce()
    {
        if (rb != null)
        {
            rb.AddForce(-transform.up * downforce * rb.linearVelocity.magnitude);
        }
    }

    private void ApplyMotor(float torque)
    {
        if (frontWheel != null) frontWheel.brakeTorque = 0f;
        if (backWheelLeft != null)
        {
            backWheelLeft.brakeTorque = 0f;
            backWheelLeft.motorTorque = torque;
        }
        if (backWheelRight != null)
        {
            backWheelRight.brakeTorque = 0f;
            backWheelRight.motorTorque = torque;
        }
    }

    private void ApplyBrakes(float torque)
    {
        if (backWheelLeft != null) backWheelLeft.motorTorque = 0f;
        if (backWheelRight != null) backWheelRight.motorTorque = 0f;

        if (frontWheel != null) frontWheel.brakeTorque = torque;
        if (backWheelLeft != null) backWheelLeft.brakeTorque = torque;
        if (backWheelRight != null) backWheelRight.brakeTorque = torque;
    }

    private void LateUpdate()
    {
        if (isDriving && playerObj != null && driverSeat != null)
        {
            playerObj.transform.position = driverSeat.position;
            playerObj.transform.rotation = driverSeat.rotation;
        }
    }

    private void SetVehicleLights(bool enable)
    {
        if (headlight != null) headlight.enabled = enable;
        if (pathLight != null) pathLight.enabled = enable;
        if (cabLight != null) cabLight.enabled = enable;
    }

    private void TriggerSonarPulse()
    {
        if (headlight != null)
        {
            headlight.intensity *= 2.5f;
            CancelInvoke(nameof(ResetHeadlight));
            Invoke(nameof(ResetHeadlight), 0.3f);
        }

        if (sonarController != null)
        {
            sonarController.TriggerPing();
        }
    }

    private void ResetHeadlight()
    {
        if (headlight != null) headlight.intensity = 3f;
    }

    private void AlertNearbyEnemies(Vector3 soundPosition, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(soundPosition, radius, enemyHitBuffer, enemyLayer);
        for (int i = 0; i < count; i++)
        {
            if (enemyHitBuffer[i] != null && enemyHitBuffer[i].TryGetComponent(out EnemyAI enemy))
            {
                enemy.AlertToSound(soundPosition, radius / Mathf.Max(1f, enemy.detectionRange));
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Zero GC allocation contact point retrieval
        Vector3 hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        CheckAndDestroyTree(collision.gameObject, hitPoint);
    }

    private void OnTriggerEnter(Collider other)
    {
        Vector3 hitPoint = GetSafeHitPoint(other, transform.position);
        CheckAndDestroyTree(other.gameObject, hitPoint);
    }

    private Vector3 GetSafeHitPoint(Collider col, Vector3 fallback)
    {
        if (col is TerrainCollider || (col is MeshCollider mc && !mc.convex))
        {
            return fallback;
        }
        return col.ClosestPoint(fallback);
    }

    private void CheckAndDestroyTree(GameObject target, Vector3 hitPoint)
    {
        if (target == gameObject || target.transform.IsChildOf(transform)) return;

        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
        if (speed < minRamSpeed) return;

        GameObject rootObj = target.transform.root.gameObject;

        // Perform fast layer and tag checks instead of dynamic string concatenation
        bool isTree = target.layer == treeLayerIndex || rootObj.layer == treeLayerIndex ||
                     target.CompareTag("Tree") || rootObj.CompareTag("Tree");

        if (isTree)
        {
            TriggerWoodEffects(hitPoint);
            Destroy(rootObj);
        }
    }

    private void TriggerWoodEffects(Vector3 position)
    {
        if (splinterPrefab != null) Instantiate(splinterPrefab, position, Quaternion.identity);
        if (woodSnapSound != null && audioSource != null) audioSource.PlayOneShot(woodSnapSound);
    }

    private void CachePlayerReferences()
    {
        if (playerObj == null)
        {
            playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerController = playerObj.GetComponent<CharacterController>();
                playerMovementScript = playerObj.GetComponent<PlayerController>();
                playerCollider = playerObj.GetComponent<Collider>();
            }
        }
    }

    private void EnterVehicle()
    {
        int collectedBatteries = BatterySpawner.Instance != null ? BatterySpawner.Instance.CollectedBatteries : 0;
        if (collectedBatteries < 2)
        {
            return;
        }

        CachePlayerReferences();
        if (playerObj == null) return;

        enterCooldown = 0.3f;

        if (playerMovementScript != null) playerMovementScript.enabled = false;
        if (playerController != null) playerController.enabled = false;
        if (playerCollider != null) playerCollider.enabled = false;

        Transform targetAnchor = driverSeat != null ? driverSeat : transform;
        playerObj.transform.position = targetAnchor.position;
        playerObj.transform.rotation = targetAnchor.rotation;

        SetVehicleLights(true);
        if (audioSource != null) audioSource.Play();
        isDriving = true;
    }

    private void ExitVehicle()
    {
        isDriving = false;
        enterCooldown = 0.3f;

        currentAccelInput = 0f;
        currentSteerInput = 0f;

        ApplyMotor(0f);
        ApplyBrakes(brakeTorque);
        SetVehicleLights(false);
        if (audioSource != null) audioSource.Stop();

        if (playerObj != null)
        {
            Transform exitAnchor = exitPoint != null ? exitPoint : transform;
            playerObj.transform.position = exitAnchor.position;
            playerObj.transform.rotation = exitAnchor.rotation;

            if (playerCollider != null) playerCollider.enabled = true;
            if (playerController != null) playerController.enabled = true;
            if (playerMovementScript != null) playerMovementScript.enabled = true;
        }
    }
}