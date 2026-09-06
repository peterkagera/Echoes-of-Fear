using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public bool isDead { get; private set; } = false;

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        // Notify all active AI entities in scene to halt footsteps and AI loops
        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI enemy in enemies)
        {
            enemy.OnPlayerDeath();
        }

        Debug.Log("Player died. All Enemy AI components disabled and footsteps silenced.");
    }
}