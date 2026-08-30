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
        if (flashlight != null)
        {
            flashlight.RechargeBattery(rechargeAmount);
            Debug.Log($"Recharged flashlight battery by {rechargeAmount}%.");
        }

        // Notify the spawner to decrement the HUD battery count
        if (BatterySpawner.Instance != null)
        {
            BatterySpawner.Instance.BatteryCollected();
        }

        Destroy(gameObject);
    }
}