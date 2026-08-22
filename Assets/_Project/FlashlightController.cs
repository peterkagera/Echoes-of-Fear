using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class FlashlightController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Light flashlightSpot;
    [SerializeField] private bool isOn = false;
    [SerializeField] private float maxBattery = 100f;
    [SerializeField] private float drainRate = 2f; // % per second

    [SerializeField] private Slider batterySlider;

    public float CurrentBattery { get; private set; }

    private void Start()
    {
        CurrentBattery = maxBattery;
        if (flashlightSpot != null)
        {
            flashlightSpot.enabled = isOn;
        }
    }

    private void Update()
    {
        if (isOn && flashlightSpot != null)
        {
            CurrentBattery -= drainRate * Time.deltaTime;
            CurrentBattery = Mathf.Clamp(CurrentBattery, 0f, maxBattery);

            // Turn off or flicker when battery empties
            if (CurrentBattery <= 0f)
            {
                isOn = false;
                flashlightSpot.enabled = false;
            }
        }
    }

    // Called automatically by PlayerInput if action named "Flashlight" or "ToggleFlashlight" exists, 
    // or trigger directly from code.
    public void OnFlashlight(InputValue value)
    {
        if (value.isPressed && CurrentBattery > 0f)
        {
            ToggleFlashlight();
        }
    }

    public void ToggleFlashlight()
    {
        AudioManager.Instance?.PlayFlashlightToggle();
        if (CurrentBattery <= 0f) return;

        isOn = !isOn;
        if (flashlightSpot != null)
        {
            flashlightSpot.enabled = isOn;
        }
    }

    public void RechargeBattery(float amount)
    {
        CurrentBattery = Mathf.Clamp(CurrentBattery + amount, 0f, maxBattery);
    }
}