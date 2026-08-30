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

        // Initialize slider properties
        if (batterySlider != null)
        {
            batterySlider.minValue = 0f;
            batterySlider.maxValue = maxBattery;
            batterySlider.value = CurrentBattery;
        }
    }

    private void Update()
    {
        if (isOn && flashlightSpot != null)
        {
            CurrentBattery -= drainRate * Time.deltaTime;
            CurrentBattery = Mathf.Clamp(CurrentBattery, 0f, maxBattery);

            // Update UI Slider each frame while draining
            UpdateUI();

            // Turn off when battery empties
            if (CurrentBattery <= 0f)
            {
                isOn = false;
                flashlightSpot.enabled = false;
            }
        }
    }

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
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (batterySlider != null)
        {
            batterySlider.value = CurrentBattery;
        }
    }
}