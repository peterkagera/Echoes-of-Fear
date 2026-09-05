using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Configuration")]
    public GameObject enemyPrefab;
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

    void Start()
    {
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

        if (enableDiagnostics)
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
            yield return new WaitForSeconds(spawnInterval);

            if (enemyPrefab == null || playerTransform == null) continue;

            Vector3 spawnPos = GetValidSpawnPosition();
            if (spawnPos != Vector3.zero)
            {
                GameObject spawnedEnemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity, transform);
                if (spawnedEnemy == null) continue;

                Renderer[] renderers = spawnedEnemy.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].enabled = false;
                }

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
                    StartCoroutine(EnableAgentSafely(agent, spawnPos, renderers));
                }
                else
                {
                    for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = true;
                }

                spawnedEnemies.Add(spawnedEnemy);
                spawnedCount++;
            }
        }
    }

    private IEnumerator EnableAgentSafely(NavMeshAgent agent, Vector3 targetPos, Renderer[] renderers)
    {
        yield return new WaitForFixedUpdate();

        if (agent != null)
        {
            agent.transform.position = targetPos;
            agent.enabled = true;
            agent.Warp(targetPos);
        }

        // Restore renderers cleanly once warp is confirmed
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = true;
            }
        }
    }

    void RecycleFarEnemies()
    {
        if (playerTransform == null) return;

        Vector3 playerPos = playerTransform.position;

        for (int i = 0; i < spawnedEnemies.Count; i++)
        {
            GameObject enemy = spawnedEnemies[i];
            if (enemy == null) continue;

            float sqrDist = (enemy.transform.position - playerPos).sqrMagnitude;

            if (sqrDist > despawnDistanceSqr)
            {
                Vector3 newPos = GetValidSpawnPosition();
                if (newPos != Vector3.zero)
                {
                    NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                    Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>();

                    for (int r = 0; r < renderers.Length; r++) renderers[r].enabled = false;

                    if (agent != null)
                    {
                        agent.enabled = false;
                        enemy.transform.position = newPos;
                        StartCoroutine(EnableAgentSafely(agent, newPos, renderers));
                    }
                    else
                    {
                        enemy.transform.position = newPos;
                        for (int r = 0; r < renderers.Length; r++) renderers[r].enabled = true;
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

        for (int i = 0; i < 15; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(minDistanceFromPlayer, spawnRadius);
            Vector3 candidatePos = playerTransform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        return Vector3.zero;
    }
}