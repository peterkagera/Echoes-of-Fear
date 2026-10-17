using UnityEngine;
using UnityEngine.Events;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("Events")]
    public UnityEvent<float, float> onHealthChanged; // Passes (currentHealth, maxHealth)
    public UnityEvent onDeath;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0f) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        Debug.Log("Player took damage. Current health: " + currentHealth);

        onHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        onHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        Debug.Log("Player has died.");
        onDeath?.Invoke();

        // Automatically activate GameOverCanvas if present in the scene
        GameObject gameOverCanvas = GameObject.Find("GameOverCanvas");
        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(true);
        }
    }
}