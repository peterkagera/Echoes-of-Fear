using UnityEngine;

public class TotemItem : MonoBehaviour
{
    [Header("Totem Settings")]
    public float pickupRange = 2.5f;
    private Transform player;
    private bool isCollected = false;

    void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    void Update()
    {
        if (player == null || isCollected) return;

        // Check distance to player
        float sqrDist = (transform.position - player.position).sqrMagnitude;
        if (sqrDist <= pickupRange * pickupRange)
        {
            CollectTotem();
        }
    }

    private void CollectTotem()
    {
        isCollected = true;

        // Wake up the player by stepping back out of the current dream layer
        if (InceptionManager.Instance != null)
        {
            InceptionManager.Instance.WakeUpFromDream();
        }

        Debug.Log("[TotemItem] Totem collected! Waking up from dream layer.");

        // Destroy the totem object
        Destroy(gameObject);
    }
}