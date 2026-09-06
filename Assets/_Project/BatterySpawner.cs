using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BatterySpawner : MonoBehaviour
{
    public static BatterySpawner Instance { get; private set; }

    // Explicit pickup counter fixing the premature Tuk-Tuk start bug
    public int CollectedBatteries { get; private set; } = 0;

    [Header("Spawn Settings")]
    public GameObject batteryPrefab;
    public int totalBatteries = 15;
    public Terrain terrain;
    public float spawnHeightOffset = 0.1f;

    [Header("Guaranteed Starter Batteries")]
    public Transform playerTransform;
    public int guaranteedNearbyCount = 2;
    public float nearbyMinRadius = 6f;
    public float nearbyMaxRadius = 18f;
    public float treeClearanceForStarter = 3.5f; // Ensures starter batteries spawn in open ground

    [Header("Playable Map Bounds")]
    public float minX = 80f;
    public float maxX = 380f;
    public float minZ = 80f;
    public float maxZ = 380f;

    [Header("Road Avoidance Settings")]
    public bool roadRunsAlongZ = true;
    public float roadXPosition = 174f;
    public float roadZPosition = 174f;
    public float roadExclusionRadius = 55f;
    public LayerMask roadLayer;
    public string roadTag = "Road";

    [Header("Tree Cluster Spawning")]
    public float minDistanceFromTree = 2.0f;
    public float maxDistanceFromTree = 6.0f;
    public float minDistanceBetweenBatteries = 18f;

    [Header("Obstacle Avoidance")]
    public float obstacleCheckRadius = 1.0f;
    public LayerMask obstacleLayers;
    public int maxSpawnAttempts = 300;

    [Header("UI HUD Reference")]
    public TextMeshProUGUI batteryCountText;

    [Header("Debug Visuals")]
    public bool drawDebugGizmos = true;

    private List<GameObject> activeBatteries = new List<GameObject>();
    private List<Vector3> cachedTreePositions = new List<Vector3>();
    private int remainingBatteries = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(Instance.gameObject);
        }
        Instance = this;
    }

    private void Start()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        CollectedBatteries = 0;
        remainingBatteries = 0;
        CollectAllTrees();
        SpawnInitialBatteries();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void CollectAllTrees()
    {
        cachedTreePositions.Clear();

        if (terrain != null && terrain.terrainData != null)
        {
            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            TreeInstance[] trees = terrain.terrainData.treeInstances;

            foreach (TreeInstance tree in trees)
            {
                Vector3 worldPos = Vector3.Scale(tree.position, terrainSize) + terrainPos;
                cachedTreePositions.Add(worldPos);
            }
        }

        GameObject[] treeObjects = GameObject.FindGameObjectsWithTag("Tree");
        foreach (GameObject obj in treeObjects)
        {
            cachedTreePositions.Add(obj.transform.position);
        }
    }

    private void SpawnInitialBatteries()
    {
        if (batteryPrefab == null)
        {
            Debug.LogError("[BatterySpawner] Battery Prefab is NOT assigned in the Inspector!");
            return;
        }

        activeBatteries.Clear();

        // 1. Ensure Player Reference exists
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }

        if (playerTransform == null)
        {
            Debug.LogError("[BatterySpawner] COULD NOT FIND PLAYER! Make sure your Player object has the tag 'Player' assigned.");
        }
        else
        {
            Debug.Log($"[BatterySpawner] Player found at position: {playerTransform.position}");
        }

        int successfullySpawned = 0;

        // 2. Guaranteed Starter Batteries Placement
        int startersToSpawn = (playerTransform != null) ? guaranteedNearbyCount : 0;
        for (int i = 0; i < startersToSpawn; i++)
        {
            // Place one slightly to the left, one to the right in front of the player
            float sideOffset = (i == 0) ? -4f : 4f;
            Vector3 starterPos = playerTransform.position + (playerTransform.forward * 10f) + (playerTransform.right * sideOffset);

            if (terrain != null)
            {
                float terrainY = terrain.SampleHeight(starterPos);
                starterPos.y = terrain.transform.position.y + terrainY + spawnHeightOffset;
            }
            else
            {
                starterPos.y = playerTransform.position.y;
            }

            GameObject starterBattery = Instantiate(batteryPrefab, starterPos, Quaternion.identity);
            activeBatteries.Add(starterBattery);
            successfullySpawned++;
            Debug.Log($"[BatterySpawner] Spawned starter battery {i + 1} at: {starterPos}");
        }

        // 3. Spawn remaining hidden map batteries
        int remainingToSpawn = totalBatteries - successfullySpawned;
        for (int i = 0; i < remainingToSpawn; i++)
        {
            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                Vector3 candidatePos = Vector3.zero;

                if (cachedTreePositions.Count > 0)
                {
                    Vector3 randomTree = cachedTreePositions[Random.Range(0, cachedTreePositions.Count)];
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float dist = Random.Range(minDistanceFromTree, maxDistanceFromTree);

                    candidatePos = new Vector3(randomTree.x + Mathf.Cos(angle) * dist, 0f, randomTree.z + Mathf.Sin(angle) * dist);
                }
                else
                {
                    candidatePos = new Vector3(Random.Range(minX, maxX), 0f, Random.Range(minZ, maxZ));
                }

                if (candidatePos.x < minX || candidatePos.x > maxX || candidatePos.z < minZ || candidatePos.z > maxZ)
                    continue;

                if (roadRunsAlongZ)
                {
                    if (Mathf.Abs(candidatePos.x - roadXPosition) < roadExclusionRadius) continue;
                }
                else
                {
                    if (Mathf.Abs(candidatePos.z - roadZPosition) < roadExclusionRadius) continue;
                }

                if (terrain != null)
                {
                    float terrainY = terrain.SampleHeight(candidatePos);
                    candidatePos.y = terrain.transform.position.y + terrainY + spawnHeightOffset;
                }

                bool tooClose = false;
                foreach (GameObject b in activeBatteries)
                {
                    if (b != null && Vector3.Distance(candidatePos, b.transform.position) < minDistanceBetweenBatteries)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                GameObject newBattery = Instantiate(batteryPrefab, candidatePos, Quaternion.identity);
                activeBatteries.Add(newBattery);
                successfullySpawned++;
                break;
            }
        }

        remainingBatteries = successfullySpawned;
        UpdateUI();
    }

    private bool IsTooCloseToTrees(Vector3 pos, float minDistance)
    {
        Vector2 pos2D = new Vector2(pos.x, pos.z);
        foreach (Vector3 treePos in cachedTreePositions)
        {
            Vector2 tree2D = new Vector2(treePos.x, treePos.z);
            if (Vector2.Distance(pos2D, tree2D) < minDistance)
            {
                return true;
            }
        }
        return false;
    }

    public Transform GetClosestBattery(Vector3 originPos, float maxDistance = 150f)
    {
        Transform closest = null;
        float minDistance = maxDistance;

        for (int i = activeBatteries.Count - 1; i >= 0; i--)
        {
            if (activeBatteries[i] == null)
            {
                activeBatteries.RemoveAt(i);
                continue;
            }

            float dist = Vector3.Distance(originPos, activeBatteries[i].transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = activeBatteries[i].transform;
            }
        }

        return closest;
    }

    public void BatteryCollected(GameObject batteryObj = null)
    {
        if (batteryObj != null && activeBatteries.Contains(batteryObj))
        {
            activeBatteries.Remove(batteryObj);
        }

        CollectedBatteries++;
        remainingBatteries = Mathf.Max(0, remainingBatteries - 1);
        UpdateUI();
    }

    public void UnregisterBattery(GameObject batteryObj)
    {
        if (batteryObj != null && activeBatteries.Contains(batteryObj))
        {
            activeBatteries.Remove(batteryObj);
        }
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
        if (!drawDebugGizmos) return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
        if (roadRunsAlongZ)
        {
            Vector3 center = new Vector3(roadXPosition, 0f, (minZ + maxZ) / 2f);
            Vector3 size = new Vector3(roadExclusionRadius * 2f, 50f, maxZ - minZ);
            Gizmos.DrawCube(center, size);
        }
        else
        {
            Vector3 center = new Vector3((minX + maxX) / 2f, 0f, roadZPosition);
            Vector3 size = new Vector3(maxX - minX, 50f, roadExclusionRadius * 2f);
            Gizmos.DrawCube(center, size);
        }

        if (activeBatteries != null)
        {
            Gizmos.color = Color.green;
            foreach (GameObject b in activeBatteries)
            {
                if (b != null) Gizmos.DrawWireSphere(b.transform.position, 1.2f);
            }
        }
    }
}