using UnityEngine;

public class ExtractionGate : MonoBehaviour
{
    [Header("UI Setup")]
    public GameObject winCanvasUI; // Drag your victory/game over canvas here

    private void OnTriggerEnter(Collider other)
    {
        bool isPlayerOnFoot = other.CompareTag("Player");
        bool isVehicle = other.GetComponentInParent<TukTukVehicle>() != null;

        if (isPlayerOnFoot || isVehicle)
        {
            if (winCanvasUI != null)
            {
                winCanvasUI.SetActive(true);
            }

            Time.timeScale = 0f; // Pause game logic on win
            Debug.Log("ESCAPED! YOU WIN!");
        }
    }
}