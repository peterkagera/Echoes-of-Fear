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

        // Recharge flashlight (if FlashlightController exists in scene)
        if (FlashlightController.Instance != null)
        {
            FlashlightController.Instance.RechargeBattery(rechargeAmount);
        }

        // Notify Spawner to decrement counter and update UI HUD
        if (BatterySpawner.Instance != null)
        {
            BatterySpawner.Instance.BatteryCollected(gameObject);
        }

        Destroy(gameObject);
    }
}