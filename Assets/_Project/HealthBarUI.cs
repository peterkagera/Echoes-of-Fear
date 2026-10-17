using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Slider healthSlider;

    private void Start()
    {
        // Automatically find PlayerHealth if unassigned
        if (playerHealth == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        // Automatically grab the Slider component on this GameObject if unassigned
        if (healthSlider == null)
        {
            healthSlider = GetComponent<Slider>();
        }

        if (playerHealth != null && healthSlider != null)
        {
            healthSlider.maxValue = playerHealth.MaxHealth;
            healthSlider.value = playerHealth.CurrentHealth;

            // Listen for health changes
            playerHealth.onHealthChanged.AddListener(UpdateHealthBar);
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.onHealthChanged.RemoveListener(UpdateHealthBar);
        }
    }

    private void UpdateHealthBar(float current, float max)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = max;
            healthSlider.value = current;
        }
    }
}