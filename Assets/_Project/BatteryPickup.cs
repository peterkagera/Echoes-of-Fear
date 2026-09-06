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
            Debug.Log($"Recharged flashlight battery by {rechargeAmount}%.");
        }

        if (BatterySpawner.Instance != null)
        {
            if (!usedForFlashlight)
            {
                // Counts toward Tuk-Tuk power cells and removes from Sonar tracking
                BatterySpawner.Instance.BatteryCollected(gameObject);
            }
            else
            {
                // Spent on flashlight: unregister from Sonar tracking without counting toward Tuk-Tuk
                BatterySpawner.Instance.UnregisterBattery(gameObject);
                Debug.Log("Battery spent powering flashlight. Excluded from Tuk-Tuk power cells.");
            }
        }

        Destroy(gameObject);
    }
}