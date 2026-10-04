using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BatterySpawner : MonoBehaviour
{
    public static BatterySpawner Instance { get; private set; }

    public int CollectedBatteries { get; private set; } = 0;

    [Header("Spawn Settings")]
    public GameObject batteryPrefab;
    public int totalBatteries = 15;
    public Terrain terrain;
    public float spawnHeightOffset = 0.1f;

    [Header("Guaranteed Starter Batteries")]
    public Transform playerTransform;
    public int guaranteedNearbyCount = 2;
    public float nearbyForwardOffset = 7.0f;
    public float nearbySideOffset = 3.5f;

    [Header("Playable Forest Bounds")]
    public float minX = 80f;
    public float maxX = 380f;
    public float minZ = 80f;
    public float maxZ = 380f;

    [Header("Road Avoidance")]
    [Tooltip("Layer assigned to your Spline / Road mesh.")]
    public LayerMask roadLayer;
    [Tooltip("Minimum distance batteries must stay away from the road.")]
    public float roadExclusionRadius = 12f;

    [Header("Tree & Spacing Settings")]
    public float minDistanceFromTree = 2.0f;
    public float maxDistanceFromTree = 6.0f;
    [Tooltip("Minimum distance between any two batteries to prevent clustering.")]
    public float minDistanceBetweenBatteries = 25f;

    [Header("Obstacle Avoidance")]
    public float obstacleCheckRadius = 1.0f;
    public LayerMask obstacleLayers;
    public int maxSpawnAttempts = 400;

    [Header("UI HUD Reference")]
    public TextMeshProUGUI batteryCountText;

    private readonly List<GameObject> activeBatteries = new List<GameObject>();
    private readonly List<Vector3> cachedTreePositions = new List<Vector3>();
    private int remainingBatteries = 0;
    private SpookyHUDCounter spookyUI;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Application.targetFrameRate = 60;
        QualitySettings.resolutionScalingFixedDPIFactor = 0.65f;
    }

    private void Start()
    {
        InitializeSpawner();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void InitializeSpawner()
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
        remainingBatteries = totalBatteries;
        UpdateUI();

        CollectAllTrees();
        SpawnInitialBatteries();
    }

    public void CollectAllTrees()
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

        for (int i = activeBatteries.Count - 1; i >= 0; i--)
        {
            if (activeBatteries[i] != null) Destroy(activeBatteries[i]);
        }
        activeBatteries.Clear();

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        int successfullySpawned = 0;

        // 1. Starter Batteries
        if (playerTransform != null)
        {
            int startersToSpawn = Mathf.Min(guaranteedNearbyCount, totalBatteries);
            Vector3 playerForward = playerTransform.forward;
            Vector3 playerRight = playerTransform.right;

            for (int i = 0; i < startersToSpawn; i++)
            {
                float sideOffset = (i == 0) ? -nearbySideOffset : nearbySideOffset;
                Vector3 starterPos = playerTransform.position + (playerForward * nearbyForwardOffset) + (playerRight * sideOffset);

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

        // 2. Forest Batteries
        int remainingToSpawn = totalBatteries - successfullySpawned;
        float minDistSqr = minDistanceBetweenBatteries * minDistanceBetweenBatteries;

        for (int i = 0; i < remainingToSpawn; i++)
        {
            bool spawned = false;

            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                Vector3 candidatePos;

                // Spawns near trees if trees exist; otherwise samples randomly across forest bounds
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

                // Bounds Check
                if (candidatePos.x < minX || candidatePos.x > maxX || candidatePos.z < minZ || candidatePos.z > maxZ) continue;

                // Dynamic Road Avoidance Check (Works on curved splines)
                if (roadLayer.value != 0 && Physics.CheckSphere(candidatePos, roadExclusionRadius, roadLayer))
                {
                    continue;
                }

                // General Obstacle Avoidance Check
                if (obstacleLayers.value != 0 && Physics.CheckSphere(candidatePos, obstacleCheckRadius, obstacleLayers))
                {
                    continue;
                }

                // Sample Terrain Height
                if (terrain != null)
                {
                    float terrainY = terrain.SampleHeight(candidatePos);
                    candidatePos.y = terrain.transform.position.y + terrainY + spawnHeightOffset;
                }

                // Spacing Check (Prevents clustering)
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
                spawned = true;
                break;
            }

            if (!spawned)
            {
                Debug.LogWarning($"[BatterySpawner] Could not find valid position for battery {successfullySpawned + 1}. Try lowering 'minDistanceBetweenBatteries' or 'roadExclusionRadius'.");
            }
        }

        remainingBatteries = totalBatteries;
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
}