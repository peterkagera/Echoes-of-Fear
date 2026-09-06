using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BatterySpawner : MonoBehaviour
{
    public static BatterySpawner Instance { get; private set; }

    public int CollectedBatteries => totalBatteries - remainingBatteries;

    [Header("Spawn Settings")]
    public GameObject batteryPrefab;
    public int totalBatteries = 20;
    public Terrain terrain;
    public float spawnHeightOffset = 0.1f;

    [Header("Playable Map Bounds")]
    public float minX = 80f;
    public float maxX = 400f;
    public float minZ = 80f;
    public float maxZ = 400f;

    [Header("Tree & Obstacle Avoidance")]
    public float treeExclusionRadius = 3.5f;     // Minimum distance from any tree trunk
    public float minDistanceBetweenBatteries = 12f;
    public float obstacleCheckRadius = 1.2f;     // Physical check radius for 3D object trees
    public LayerMask obstacleLayers;            // Set to Default or Tree layer in Inspector
    public int maxSpawnAttempts = 150;

    [Header("UI HUD Reference")]
    public TextMeshProUGUI batteryCountText;

    [Header("Debug Visuals")]
    public bool drawDebugGizmos = true;

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
        Vector3 terrainSize = (terrain != null && terrain.terrainData != null) ? terrain.terrainData.size : Vector3.zero;
        TreeInstance[] terrainTrees = (terrain != null && terrain.terrainData != null) ? terrain.terrainData.treeInstances : new TreeInstance[0];

        for (int i = 0; i < totalBatteries; i++)
        {
            bool spawned = false;
            float currentMinDistance = minDistanceBetweenBatteries;
            float currentTreeRadius = treeExclusionRadius;

            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                if (attempt > maxSpawnAttempts / 2)
                {
                    currentMinDistance *= 0.7f;
                    currentTreeRadius *= 0.8f;
                }

                float randomX = Random.Range(minX, maxX);
                float randomZ = Random.Range(minZ, maxZ);

                // 1. Direct Ground Sampling (Bypasses tree canopy top-down collisions)
                float terrainY = terrain.SampleHeight(new Vector3(randomX, 0, randomZ));
                Vector3 candidatePos = new Vector3(randomX, terrainPos.y + terrainY + spawnHeightOffset, randomZ);

                // 2. Check Unity Terrain Painted Trees
                bool insideTerrainTree = false;
                for (int t = 0; t < terrainTrees.Length; t++)
                {
                    Vector3 worldTreePos = Vector3.Scale(terrainTrees[t].position, terrainSize) + terrainPos;
                    float dist2D = Vector2.Distance(new Vector2(candidatePos.x, candidatePos.z), new Vector2(worldTreePos.x, worldTreePos.z));

                    if (dist2D < currentTreeRadius)
                    {
                        insideTerrainTree = true;
                        break;
                    }
                }
                if (insideTerrainTree) continue;

                // 3. Physical Obstacle Check (For GameObject-based trees / colliders)
                if (obstacleLayers.value != 0)
                {
                    if (Physics.CheckSphere(candidatePos + Vector3.up * 0.8f, obstacleCheckRadius, obstacleLayers))
                        continue;

                    // Upward clearance raycast to ensure no low hanging branches overhead
                    if (Physics.Raycast(candidatePos + Vector3.up * 0.2f, Vector3.up, 5.0f, obstacleLayers))
                        continue;
                }

                // 4. Check Distance from Other Batteries
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

                // Spawn Validated Battery
                Instantiate(batteryPrefab, candidatePos, Quaternion.identity);
                spawnedPositions.Add(candidatePos);
                successfullySpawned++;
                spawned = true;
                break;
            }

            if (!spawned)
            {
                Debug.LogWarning($"BatterySpawner: Could not find clear spot for battery #{i + 1}.");
            }
        }

        remainingBatteries = successfullySpawned;
        Debug.Log($"BatterySpawner: Successfully spawned {successfullySpawned} / {totalBatteries} clear batteries.");
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

    private void OnDrawGizmos()
    {
        if (!drawDebugGizmos || spawnedPositions == null) return;

        Gizmos.color = Color.green;
        foreach (Vector3 pos in spawnedPositions)
        {
            Gizmos.DrawWireSphere(pos, 1.2f);
        }
    }
}