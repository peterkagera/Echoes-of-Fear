using System.Collections.Generic;
using UnityEngine;
using TMPro; // Remove if using standard UI Text

public class BatterySpawner : MonoBehaviour
{
    public static BatterySpawner Instance { get; private set; }

    [Header("Spawn Settings")]
    public GameObject batteryPrefab;
    public int totalBatteries = 15;
    public Terrain terrain;
    public float raycastStartHeight = 100f;
    public float spawnHeightOffset = 0.1f;

    [Header("Full Map Bounds")]
    public float minX = 10f;
    public float maxX = 490f;
    public float minZ = 10f;
    public float maxZ = 490f;

    [Header("Distribution Controls")]
    public float minDistanceBetweenBatteries = 15f;
    public float obstacleCheckRadius = 0.8f;
    public LayerMask obstacleLayers;
    public LayerMask groundLayer;
    public int maxSpawnAttempts = 100;

    [Header("UI HUD Reference")]
    public TextMeshProUGUI batteryCountText; // Change to 'public Text batteryCountText;' if using legacy UI

    private List<Vector3> spawnedPositions = new List<Vector3>();
    private int remainingBatteries = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        SpawnInitialBatteries();
    }

    private void SpawnInitialBatteries()
    {
        if (batteryPrefab == null) return;

        spawnedPositions.Clear();
        int successfullySpawned = 0;
        Vector3 terrainPos = (terrain != null) ? terrain.transform.position : Vector3.zero;

        for (int i = 0; i < totalBatteries; i++)
        {
            bool spawned = false;
            float currentMinDistance = minDistanceBetweenBatteries;

            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                if (attempt > maxSpawnAttempts / 2)
                {
                    currentMinDistance *= 0.8f;
                }

                float randomX = Random.Range(minX, maxX);
                float randomZ = Random.Range(minZ, maxZ);

                Vector3 candidatePos = Vector3.zero;
                bool foundHeight = false;

                // Priority 1: Raycast Ground Check
                if (groundLayer.value != 0)
                {
                    Vector3 rayOrigin = new Vector3(randomX, raycastStartHeight, randomZ);
                    if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastStartHeight + 100f, groundLayer))
                    {
                        candidatePos = hit.point + (Vector3.up * spawnHeightOffset);
                        foundHeight = true;
                    }
                }

                // Priority 2: Direct Terrain Height Fallback
                if (!foundHeight && terrain != null)
                {
                    float worldX = terrainPos.x + randomX;
                    float worldZ = terrainPos.z + randomZ;
                    float terrainY = terrain.SampleHeight(new Vector3(worldX, 0, worldZ));

                    candidatePos = new Vector3(worldX, terrainPos.y + terrainY + spawnHeightOffset, worldZ);
                    foundHeight = true;
                }

                if (!foundHeight) continue;

                // Obstacle check
                if (obstacleLayers.value != 0 && Physics.CheckSphere(candidatePos + Vector3.up * 0.5f, obstacleCheckRadius, obstacleLayers))
                    continue;

                // Distance check
                bool tooClose = false;
                foreach (Vector3 existingPos in spawnedPositions)
                {
                    if (Vector3.Distance(candidatePos, existingPos) < currentMinDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose) continue;

                Instantiate(batteryPrefab, candidatePos, Quaternion.identity);
                spawnedPositions.Add(candidatePos);
                successfullySpawned++;
                spawned = true;
                break;
            }

            if (!spawned)
            {
                Debug.LogWarning($"BatterySpawner: Fallback spawn triggered for battery #{i + 1}.");
            }
        }

        remainingBatteries = successfullySpawned;
        Debug.Log($"BatterySpawner: Successfully spawned {successfullySpawned} / {totalBatteries} batteries on the map.");
        UpdateUI();
    }

    public void BatteryCollected()
    {
        remainingBatteries = Mathf.Max(0, remainingBatteries - 1);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (batteryCountText != null)
        {
            SpookyHUDCounter spookyUI = batteryCountText.GetComponent<SpookyHUDCounter>();
            if (spookyUI != null)
            {
                spookyUI.UpdateCount(remainingBatteries, totalBatteries);
            }
            else
            {
                batteryCountText.text = $"POWER CELLS: {remainingBatteries} / {totalBatteries}";
            }
        }
    }
}