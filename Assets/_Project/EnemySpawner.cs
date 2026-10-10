using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Configuration")]
    public GameObject[] enemyPrefabs;
    public Transform playerTransform;
    public int totalEnemiesToSpawn = 10;
    public float spawnInterval = 3.0f;

    [Header("Spawn Bounds & Distances")]
    public float minDistanceFromPlayer = 15f;
    public float spawnRadius = 35f;
    public float despawnDistance = 65f;

    [Header("Diagnostics")]
    public bool enableDiagnostics = false;

    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();
    private float recycleTimer = 0f;
    private float despawnDistanceSqr;

    private readonly List<Renderer> reusableRendererList = new List<Renderer>();
    private int walkableAreaMask;

    void Start()
    {
        walkableAreaMask = 1 << NavMesh.GetAreaFromName("Walkable");

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            else
            {
                Debug.LogError("[EnemySpawner] Critical Error: Player transform could not be found! Tag your player object as 'Player'.");
            }
        }

        despawnDistanceSqr = despawnDistance * despawnDistance;

        if (enableDiagnostics && playerTransform != null)
        {
            Debug.Log($"[EnemySpawner] Start total={totalEnemiesToSpawn}, interval={spawnInterval:F2}, " +
                $"minDistance={minDistanceFromPlayer:F2}, spawnRadius={spawnRadius:F2}, " +
                $"despawnDistance={despawnDistance:F2}, player={playerTransform.position}", this);
        }

        StartCoroutine(SpawnEnemiesOverTime());
    }

    void Update()
    {
        recycleTimer += Time.deltaTime;
        if (recycleTimer >= 2.0f)
        {
            recycleTimer = 0f;
            RecycleFarEnemies();
        }
    }

    IEnumerator SpawnEnemiesOverTime()
    {
        int spawnedCount = 0;
        while (spawnedCount < totalEnemiesToSpawn)
        {
            yield return new WaitForSeconds(spawnInterval + Random.Range(-0.5f, 0.5f));

            if (enemyPrefabs == null || playerTransform == null) continue;

            Vector3 spawnPos = GetValidSpawnPosition();
            if (spawnPos != Vector3.zero)
            {
                GameObject chosenPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
                GameObject spawnedEnemy = Instantiate(chosenPrefab, spawnPos, Quaternion.identity, transform);
                if (spawnedEnemy == null) continue;

                SetRenderersEnabled(spawnedEnemy, false);

                EnemyAI aiScript = spawnedEnemy.GetComponent<EnemyAI>();
                if (aiScript != null)
                {
                    aiScript.player = playerTransform;
                    aiScript.enableDiagnostics = enableDiagnostics;
                }

                NavMeshAgent agent = spawnedEnemy.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.enabled = false;
                    spawnedEnemy.transform.position = spawnPos;
                    StartCoroutine(EnableAgentSafely(spawnedEnemy, agent, spawnPos));
                }
                else
                {
                    SetRenderersEnabled(spawnedEnemy, true);
                }

                spawnedEnemies.Add(spawnedEnemy);
                spawnedCount++;
            }
        }
    }

    private void SetRenderersEnabled(GameObject target, bool enabledState)
    {
        target.GetComponentsInChildren(true, reusableRendererList);
        for (int i = 0; i < reusableRendererList.Count; i++)
        {
            if (reusableRendererList[i] != null)
                reusableRendererList[i].enabled = enabledState;
        }
    }

    private IEnumerator EnableAgentSafely(GameObject enemyObj, NavMeshAgent agent, Vector3 targetPos)
    {
        yield return new WaitForFixedUpdate();

        if (agent != null)
        {
            agent.transform.position = targetPos;
            agent.enabled = true;
            agent.Warp(targetPos);
        }

        if (enemyObj != null)
        {
            SetRenderersEnabled(enemyObj, true);
        }
    }

    void RecycleFarEnemies()
    {
        if (playerTransform == null) return;

        Vector3 playerPos = playerTransform.position;

        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemy = spawnedEnemies[i];
            if (enemy == null)
            {
                spawnedEnemies.RemoveAt(i);
                continue;
            }

            float sqrDist = (enemy.transform.position - playerPos).sqrMagnitude;
            if (sqrDist > despawnDistanceSqr)
            {
                Vector3 newPos = GetValidSpawnPosition();
                if (newPos != Vector3.zero)
                {
                    NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                    EnemyAI aiScript = enemy.GetComponent<EnemyAI>();

                    SetRenderersEnabled(enemy, false);

                    if (agent != null)
                    {
                        agent.enabled = false;
                        enemy.transform.position = newPos;
                        StartCoroutine(EnableAgentSafely(enemy, agent, newPos));
                    }
                    else
                    {
                        enemy.transform.position = newPos;
                        SetRenderersEnabled(enemy, true);
                    }

                    // Re-trigger the stabilization delay when recycled
                    if (aiScript != null)
                    {
                        aiScript.ResetSpawnDelay();
                    }

                    if (enableDiagnostics)
                    {
                        Debug.Log($"[EnemySpawner] Recycled {enemy.name} to new position {newPos}", enemy);
                    }
                }
            }
        }
    }

    Vector3 GetValidSpawnPosition()
    {
        if (playerTransform == null) return Vector3.zero;

        for (int i = 0; i < 10; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(minDistanceFromPlayer, spawnRadius);
            Vector3 candidatePos = playerTransform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, 4.0f, walkableAreaMask))
            {
                return hit.position;
            }
        }

        return Vector3.zero;
    }
}