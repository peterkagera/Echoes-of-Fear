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
    public float motorTorque = 15000f;
    public float maxSteerAngle = 35f;
    public float brakeTorque = 3000f;
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.8f, 0f);

    [Header("Tree Battering Ram")]
    public float minRamSpeed = 0.5f;
    public GameObject splinterPrefab;
    public AudioClip woodSnapSound;

    [Header("Sonar High-Beams")]
    public Light headlight;
    public AudioSource audioSource;

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
            rb.linearDamping = 0.5f; // Increased slightly to prevent unnatural sliding
        }

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
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

        // Reset inputs every frame
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
        if (!isDriving) return;

        if (frontWheel != null) frontWheel.steerAngle = currentSteerInput * maxSteerAngle;

        if (currentAccelInput != 0f)
        {
            // Driving forward or backward
            if (backWheelLeft != null)
            {
                backWheelLeft.motorTorque = currentAccelInput * motorTorque;
                backWheelLeft.brakeTorque = 0f;
            }
            if (backWheelRight != null)
            {
                backWheelRight.motorTorque = currentAccelInput * motorTorque;
                backWheelRight.brakeTorque = 0f;
            }
        }
        else
        {
            // Idle / Key Released: Apply full brake torque to stop forward momentum immediately
            if (backWheelLeft != null)
            {
                backWheelLeft.motorTorque = 0f;
                backWheelLeft.brakeTorque = brakeTorque;
            }
            if (backWheelRight != null)
            {
                backWheelRight.motorTorque = 0f;
                backWheelRight.brakeTorque = brakeTorque;
            }
        }
    }

    private void LateUpdate()
    {
        if (isDriving && playerObj != null && driverSeat != null)
        {
            playerObj.transform.position = driverSeat.position;
            playerObj.transform.rotation = driverSeat.rotation;
        }
    }

    private void TriggerSonarPulse()
    {
        if (headlight != null)
        {
            headlight.enabled = true;
            headlight.intensity = 8f;
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
        if (headlight != null) headlight.intensity = 2f;
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

        // Destroy Standalone Tree GameObjects (Prefabs)
        if (target.layer == treeLayer || target.name.ToLower().Contains("ash") || target.name.ToLower().Contains("birch"))
        {
            TriggerWoodEffects(hitPoint);
            Destroy(target);
        }
    }

    private void TriggerWoodEffects(Vector3 position)
    {
        if (splinterPrefab != null) Instantiate(splinterPrefab, position, Quaternion.identity);
        if (woodSnapSound != null && audioSource != null) audioSource.PlayOneShot(woodSnapSound);
    }

    private void EnterVehicle()
    {
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

        isDriving = true;
    }

    private void ExitVehicle()
    {
        isDriving = false;
        enterCooldown = 0.3f;

        currentAccelInput = 0f;
        currentSteerInput = 0f;

        if (backWheelLeft != null)
        {
            backWheelLeft.motorTorque = 0f;
            backWheelLeft.brakeTorque = brakeTorque;
        }
        if (backWheelRight != null)
        {
            backWheelRight.motorTorque = 0f;
            backWheelRight.brakeTorque = brakeTorque;
        }

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