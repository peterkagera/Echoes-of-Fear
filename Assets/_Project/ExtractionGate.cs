using UnityEngine;

public class ExtractionGate : MonoBehaviour
{
    [Header("UI Setup")]
    public GameObject winCanvasUI;

    [Header("Victory Requirements")]
    public int requiredBatteries = 15;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Check if player has collected enough batteries
            if (BatterySpawner.Instance != null)
            {
                if (BatterySpawner.Instance.CollectedBatteries >= requiredBatteries)
                {
                    TriggerVictory();
                }
                else
                {
                    Debug.Log($"Gate locked! Collected {BatterySpawner.Instance.CollectedBatteries}/{requiredBatteries} batteries.");
                }
            }
            else
            {
                // Fallback if spawner doesn't exist
                TriggerVictory();
            }
        }
    }

    public void TriggerVictory()
    {
        // 1. Show the Victory Screen UI
        if (winCanvasUI != null)
        {
            winCanvasUI.SetActive(true);
        }

        // 2. Unlock and show the mouse cursor for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3. Pause game time
        Time.timeScale = 0f;

        // 4. Disable player movement/looking
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            player.enabled = false;
        }
    }
}