using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class FlashlightController : MonoBehaviour
{
    public static FlashlightController Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private Light flashlightSpot;
    [SerializeField] private bool isOn = false;
    [SerializeField] private float maxBattery = 100f;
    [SerializeField] private float drainRate = 2f; // % per second

    [Header("UI References")]
    [SerializeField] private Slider batterySlider;
    [SerializeField] private TextMeshProUGUI batteryPercentText;

    public float CurrentBattery { get; private set; }
    public bool IsOn => isOn;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        ResetFlashlight();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        // 1. Direct PC Keyboard 'F' key tap listener
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            ToggleFlashlight();
        }

        // 2. Battery Drain logic while light is ON
        if (!isOn) return;

        if (CurrentBattery > 0f)
        {
            CurrentBattery -= drainRate * Time.deltaTime;
            CurrentBattery = Mathf.Clamp(CurrentBattery, 0f, maxBattery);

            UpdateUI();

            if (CurrentBattery <= 0f)
            {
                TurnOff();
            }
        }
    }

    // Called on single-tap of Mobile UI Button or PC 'F' Key
    public void ToggleFlashlight()
    {
        if (isOn)
        {
            TurnOff();
        }
        else if (CurrentBattery > 0f)
        {
            TurnOn();
        }
    }

    public void TurnOn()
    {
        if (CurrentBattery <= 0f) return;

        isOn = true;
        if (flashlightSpot != null) flashlightSpot.enabled = true;
        AudioManager.Instance?.PlayFlashlightToggle();
        UpdateUI(true);
    }

    public void TurnOff()
    {
        isOn = false;
        if (flashlightSpot != null) flashlightSpot.enabled = false;
        AudioManager.Instance?.PlayFlashlightToggle();
        UpdateUI(true);
    }

    public void RechargeBattery(float amount)
    {
        CurrentBattery = Mathf.Clamp(CurrentBattery + amount, 0f, maxBattery);
        UpdateUI(true);
    }

    public void ResetFlashlight()
    {
        CurrentBattery = maxBattery;
        isOn = false;

        if (flashlightSpot != null)
        {
            flashlightSpot.enabled = false;
        }

        if (batterySlider != null)
        {
            batterySlider.minValue = 0f;
            batterySlider.maxValue = maxBattery;
        }

        UpdateUI(true);
    }

    private void UpdateUI(bool force = false)
    {
        if (batterySlider != null)
        {
            batterySlider.value = CurrentBattery;
        }

        if (batteryPercentText != null)
        {
            int percent = Mathf.CeilToInt((CurrentBattery / maxBattery) * 100f);
            batteryPercentText.text = $"{percent}%";
        }
    }
}