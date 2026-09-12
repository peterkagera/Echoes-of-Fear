using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public bool isDead { get; private set; } = false;

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null)
            {
                enemies[i].OnPlayerDeath();
            }
        }
    }
}