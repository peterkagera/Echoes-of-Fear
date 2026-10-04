using UnityEngine;

public class ExtractionGate : MonoBehaviour
{
    [Header("UI Setup")]
    public GameObject winCanvasUI;

    [Header("Victory Requirements")]
    public int requiredBatteries = 5;

    private bool isVictorious = false;

    private void OnTriggerEnter(Collider other)
    {
        CheckVictoryCondition(other);
    }

    private void OnTriggerStay(Collider other)
    {
        CheckVictoryCondition(other);
    }

    private void CheckVictoryCondition(Collider other)
    {
        if (isVictorious) return;

        // Detect player directly, via parent/child hierarchy, or via vehicle
        bool isPlayerOrVehicle = other.CompareTag("Player") ||
                                 other.GetComponent<PlayerController>() != null ||
                                 other.GetComponentInParent<PlayerController>() != null ||
                                 other.GetComponentInChildren<PlayerController>() != null ||
                                 other.GetComponent<TukTukVehicle>() != null ||
                                 other.GetComponentInParent<TukTukVehicle>() != null ||
                                 other.GetComponentInChildren<TukTukVehicle>() != null;

        if (isPlayerOrVehicle)
        {
            int currentBatteries = (BatterySpawner.Instance != null)
                ? BatterySpawner.Instance.CollectedBatteries
                : requiredBatteries;

            if (currentBatteries >= requiredBatteries)
            {
                TriggerVictory();
            }
        }
    }

    public void TriggerVictory()
    {
        if (isVictorious) return;
        isVictorious = true;

        Debug.Log("[ExtractionGate] Victory condition met! Triggering Win Screen.");

        if (winCanvasUI != null)
        {
            winCanvasUI.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Time.timeScale = 0f;

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            player.enabled = false;
        }
    }
}