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
    public float brakeTorque = 25000f;      // Strong brake force
    public float idleBrakeTorque = 8000f;   // Strong drag on release
    public float maxSteerAngle = 42f;
    public float steerSpeed = 120f;         // Fast turn rate
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.8f, 0f);

    [Header("Path & Headlight Vision")]
    public Light headlight;
    public Light pathLight;
    public Light cabLight;
    public AudioSource audioSource;

    [Header("Tree Battering Ram")]
    public float minRamSpeed = 0.5f;
    public GameObject splinterPrefab;
    public AudioClip woodSnapSound;

    private bool isDriving = false;
    private float enterCooldown = 0f;
    private GameObject playerObj;
    private CharacterController playerController;
    private PlayerController playerMovementScript;
    private Collider playerCollider;
    private Rigidbody rb;

    private float currentSteerInput = 0f;
    private float currentAccelInput = 0f;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.centerOfMass = centerOfMassOffset;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearDamping = 0.15f;
            rb.angularDamping = 2.0f;
        }

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        SetVehicleLights(false);
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

        float targetSteerAngle = currentSteerInput * maxSteerAngle;
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
            if (forwardSpeed < -0.5f)
            {
                ApplyBrakes(brakeTorque);
            }
            else
            {
                ApplyMotor(currentAccelInput * motorTorque);
            }
        }
        else if (currentAccelInput < 0f)
        {
            if (forwardSpeed > 0.5f)
            {
                ApplyBrakes(brakeTorque);
            }
            else
            {
                ApplyMotor(currentAccelInput * motorTorque);
            }
        }
        else
        {
            ApplyBrakes(idleBrakeTorque);
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
            float origIntensity = headlight.intensity;
            headlight.intensity = origIntensity * 2.5f;
            Invoke(nameof(ResetHeadlight), 0.3f);
        }

        GameObject sonarObj = GameObject.Find("SonarOverlay");
        if (sonarObj != null)
        {
            sonarObj.SendMessage("TriggerPulse", SendMessageOptions.DontRequireReceiver);
        }
    }

    private void ResetHeadlight()
    {
        if (headlight != null) headlight.intensity = 3f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
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

        int treeLayer = LayerMask.NameToLayer("Tree");
        GameObject rootObj = target.transform.root.gameObject;

        bool isTreeLayer = target.layer == treeLayer || rootObj.layer == treeLayer;
        string combinedName = (target.name + " " + rootObj.name).ToLower();
        bool isTreeName = combinedName.Contains("tree") || combinedName.Contains("ash") || combinedName.Contains("birch") || combinedName.Contains("particle_tree");

        if (isTreeLayer || isTreeName)
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

    private void EnterVehicle()
    {
        // Check if player has collected at least 2 batteries via BatterySpawner
        int collectedBatteries = BatterySpawner.Instance != null ? BatterySpawner.Instance.CollectedBatteries : 0;
        if (collectedBatteries < 2)
        {
            Debug.Log($"Engine won't start! Scavenged {collectedBatteries}/2 required batteries.");
            return;
        }

        playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        enterCooldown = 0.3f;

        playerController = playerObj.GetComponent<CharacterController>();
        playerMovementScript = playerObj.GetComponent<PlayerController>();
        playerCollider = playerObj.GetComponent<Collider>();

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