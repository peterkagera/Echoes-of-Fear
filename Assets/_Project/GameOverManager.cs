using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject gameOverCanvas;

    public void ShowGameOver()
    {
        Debug.LogWarning($"[GameOverManager] ShowGameOver invoked. " +
                         $"time={Time.time:F2}, unscaledTime={Time.unscaledTime:F2}, " +
                         $"previousTimeScale={Time.timeScale:F2}, " +
                         $"canvasWasActive={gameOverCanvas != null && gameOverCanvas.activeSelf}", this);

        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(true);
            Debug.Log($"[GameOverManager] Game-over canvas activated: {gameOverCanvas.name}.", gameOverCanvas);
        }
        else
        {
            Debug.LogError("[GameOverManager] gameOverCanvas is null; UI cannot be displayed.", this);
        }

        // Freeze world simulation
        Time.timeScale = 0f;
        Debug.Log("[GameOverManager] Time.timeScale set to 0.", this);

        // Stop player look/movement input and unlock cursor
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            Debug.Log($"[GameOverManager] Player found: {player.name}. " +
                      $"enabledBefore={player.enabled}, position={player.transform.position}.", player);
            player.UnlockCursor();
            player.enabled = false;
            Debug.Log($"[GameOverManager] Player look/movement disabled: enabledAfter={player.enabled}.", player);
        }
        else
        {
            Debug.LogWarning("[GameOverManager] PlayerController not found; only cursor fallback will be applied.", this);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void RestartGame()
    {
        Debug.Log("[GameOverManager] RestartGame invoked; restoring time scale and reloading scene.", this);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Debug.Log("[GameOverManager] QuitGame invoked; restoring time scale and quitting application.", this);
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }
}