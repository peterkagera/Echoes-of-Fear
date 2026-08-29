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
    public float minDistanceFromPlayer = 12f;
    public float spawnRadius = 30f;
    public float despawnDistance = 45f;

    private List<GameObject> spawnedEnemies = new List<GameObject>();
    private float recycleTimer = 0f;

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
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

            if (enemyPrefab == null || playerTransform == null) yield break;

            Vector3 spawnPos = GetValidSpawnPosition();
            if (spawnPos != Vector3.zero)
            {
                GameObject spawnedEnemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity, transform);

                NavMeshAgent agent = spawnedEnemy.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.Warp(spawnPos);
                }

                EnemyAI aiScript = spawnedEnemy.GetComponent<EnemyAI>();
                if (aiScript != null)
                {
                    aiScript.player = playerTransform;
                    aiScript.randomizeSpawnOnStart = false;
                }

                spawnedEnemies.Add(spawnedEnemy);
                spawnedCount++;
            }
        }
    }

    void RecycleFarEnemies()
    {
        if (playerTransform == null) return;

        foreach (GameObject enemy in spawnedEnemies)
        {
            if (enemy == null) continue;

            float dist = Vector3.Distance(enemy.transform.position, playerTransform.position);

            if (dist > despawnDistance)
            {
                Vector3 newPos = GetValidSpawnPosition();
                if (newPos != Vector3.zero)
                {
                    NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                    if (agent != null)
                    {
                        agent.Warp(newPos);
                    }
                    else
                    {
                        enemy.transform.position = newPos;
                    }
                }
            }
        }
    }

    Vector3 GetValidSpawnPosition()
    {
        Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(minDistanceFromPlayer, spawnRadius);
        Vector3 candidatePos = playerTransform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

        if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, 10.0f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return Vector3.zero;
    }
}