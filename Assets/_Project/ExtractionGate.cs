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
            if (BatterySpawner.Instance != null)
            {
                if (BatterySpawner.Instance.CollectedBatteries >= requiredBatteries)
                {
                    TriggerVictory();
                }
            }
            else
            {
                TriggerVictory();
            }
        }
    }

    public void TriggerVictory()
    {
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