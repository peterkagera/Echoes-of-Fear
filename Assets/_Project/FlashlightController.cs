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
    public bool IsOn => isOn;

    private void Start()
    {
        CurrentBattery = maxBattery;
        if (flashlightSpot != null)
        {
            flashlightSpot.enabled = isOn;
        }
        if (batterySlider != null)
        {
            batterySlider.minValue = 0f;
            batterySlider.maxValue = maxBattery;
            UpdateUI(true);
        }
    }

    private void Update()
    {
        if (isOn && flashlightSpot != null)
        {
            CurrentBattery -= drainRate * Time.deltaTime;
            CurrentBattery = Mathf.Clamp(CurrentBattery, 0f, maxBattery);

            UpdateUI();

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
        UpdateUI(true);
    }

    private void UpdateUI(bool force = false)
    {
        if (batterySlider != null)
        {
            // Only force canvas rebuilds when battery level changes by >0.5% or on forced events
            if (force || Mathf.Abs(batterySlider.value - CurrentBattery) > 0.5f)
            {
                batterySlider.value = CurrentBattery;
            }
        }
    }
}