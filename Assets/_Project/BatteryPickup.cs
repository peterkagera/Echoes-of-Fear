using UnityEngine;

public class BatteryPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private float rechargeAmount = 50f;

    public string GetPrompt()
    {
        return "Pick up Battery";
    }

    public void Interact()
    {
        AudioManager.Instance?.PlayBatteryPickup();
        FlashlightController flashlight = FindAnyObjectByType<FlashlightController>();
        bool usedForFlashlight = false;

        if (flashlight != null)
        {
            if (flashlight.IsOn)
            {
                usedForFlashlight = true;
            }
            flashlight.RechargeBattery(rechargeAmount);
        }

        if (BatterySpawner.Instance != null)
        {
            if (!usedForFlashlight)
            {
                BatterySpawner.Instance.BatteryCollected(gameObject);
            }
            else
            {
                BatterySpawner.Instance.UnregisterBattery(gameObject);
            }
        }

        Destroy(gameObject);
    }
}