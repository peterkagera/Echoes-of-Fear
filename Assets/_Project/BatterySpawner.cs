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
    public float treeClearanceForStarter = 3.5f;

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
    public bool drawDebugGizmos = false;

    private readonly List<GameObject> activeBatteries = new List<GameObject>();
    private readonly List<Vector3> cachedTreePositions = new List<Vector3>();
    private int remainingBatteries = 0;
    private SpookyHUDCounter spookyUI;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(Instance.gameObject);
            return;
        }
        Instance = this;

        // Mobile performance caps
        Application.targetFrameRate = 60;
        QualitySettings.resolutionScalingFixedDPIFactor = 0.65f;
    }

    private void Start()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        if (batteryCountText != null)
        {
            spookyUI = batteryCountText.GetComponent<SpookyHUDCounter>();
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

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        int successfullySpawned = 0;

        // 1. Guaranteed Starter Batteries right in front of the player view
        if (playerTransform != null)
        {
            int startersToSpawn = Mathf.Min(guaranteedNearbyCount, totalBatteries);
            Vector3 playerForward = playerTransform.forward;
            Vector3 playerRight = playerTransform.right;

            for (int i = 0; i < startersToSpawn; i++)
            {
                float sideOffset = (i == 0) ? -3.5f : 3.5f;
                float forwardOffset = 7.0f; // Placed 7 meters directly ahead in view
                Vector3 starterPos = playerTransform.position + (playerForward * forwardOffset) + (playerRight * sideOffset);

                if (terrain != null)
                {
                    float terrainY = terrain.SampleHeight(starterPos);
                    starterPos.y = terrain.transform.position.y + terrainY + spawnHeightOffset;
                }
                else
                {
                    starterPos.y = playerTransform.position.y + spawnHeightOffset;
                }

                GameObject starterBattery = Instantiate(batteryPrefab, starterPos, Quaternion.identity);
                activeBatteries.Add(starterBattery);
                successfullySpawned++;
            }
        }

        // 2. Spawn remaining hidden map batteries
        int remainingToSpawn = totalBatteries - successfullySpawned;
        float minDistSqr = minDistanceBetweenBatteries * minDistanceBetweenBatteries;

        for (int i = 0; i < remainingToSpawn; i++)
        {
            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                Vector3 candidatePos;
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

                if (candidatePos.x < minX || candidatePos.x > maxX || candidatePos.z < minZ || candidatePos.z > maxZ) continue;

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
                for (int bIdx = 0; bIdx < activeBatteries.Count; bIdx++)
                {
                    GameObject b = activeBatteries[bIdx];
                    if (b != null && (candidatePos - b.transform.position).sqrMagnitude < minDistSqr)
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

    public Transform GetClosestBattery(Vector3 originPos, float maxDistance = 150f)
    {
        Transform closest = null;
        float minSqrDistance = maxDistance * maxDistance;

        for (int i = activeBatteries.Count - 1; i >= 0; i--)
        {
            if (activeBatteries[i] == null)
            {
                activeBatteries.RemoveAt(i);
                continue;
            }

            float sqrDist = (originPos - activeBatteries[i].transform.position).sqrMagnitude;
            if (sqrDist < minSqrDistance)
            {
                minSqrDistance = sqrDist;
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
        if (batteryCountText == null) return;

        if (spookyUI != null)
        {
            spookyUI.UpdateCount(remainingBatteries, totalBatteries);
        }
        else
        {
            batteryCountText.text = $"POWER CELLS: {remainingBatteries} / {totalBatteries}";
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