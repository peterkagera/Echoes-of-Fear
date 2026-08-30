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

            if (enemyPrefab == null) yield break;
            if (playerTransform == null) continue;

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

                NavMeshAgent agent = spawnedEnemy.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.enabled = false;
                    spawnedEnemy.transform.position = spawnPos;
                    StartCoroutine(EnableAgentSafely(agent, spawnedEnemy.GetComponent<EnemyAI>(), renderers));
                }
                else
                {
                    for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = true;
                }

                EnemyAI aiScript = spawnedEnemy.GetComponent<EnemyAI>();
                if (aiScript != null)
                {
                    aiScript.player = playerTransform;
                    aiScript.randomizeSpawnOnStart = false;
                    aiScript.SetState(EnemyAI.AIState.Dormant);
                }

                spawnedEnemies.Add(spawnedEnemy);
                spawnedCount++;
            }
        }
    }

    private IEnumerator EnableAgentSafely(NavMeshAgent agent, EnemyAI aiScript, Renderer[] renderers)
    {
        yield return null;
        yield return null;

        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(agent.transform.position);
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].enabled = true;
        }

        if (aiScript != null)
        {
            aiScript.SetState(EnemyAI.AIState.Dormant);
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
                    EnemyAI aiScript = enemy.GetComponent<EnemyAI>();
                    Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>();

                    for (int r = 0; r < renderers.Length; r++) renderers[r].enabled = false;

                    if (agent != null)
                    {
                        agent.enabled = false;
                        enemy.transform.position = newPos;
                        StartCoroutine(EnableAgentSafely(agent, aiScript, renderers));
                    }
                    else
                    {
                        enemy.transform.position = newPos;
                        for (int r = 0; r < renderers.Length; r++) renderers[r].enabled = true;
                    }
                }
            }
        }
    }

    Vector3 GetValidSpawnPosition()
    {
        if (playerTransform == null) return Vector3.zero;

        Camera mainCam = Camera.main;
        Transform lookTransform = mainCam != null ? mainCam.transform : playerTransform;

        for (int i = 0; i < 10; i++)
        {
            float randomAngleDeg = Random.Range(-75f, 75f);
            Quaternion rotation = Quaternion.Euler(0f, randomAngleDeg, 0f);
            Vector3 forwardDirection = rotation * new Vector3(lookTransform.forward.x, 0f, lookTransform.forward.z).normalized;

            float randomDistance = Random.Range(minDistanceFromPlayer, spawnRadius);
            Vector3 candidatePos = playerTransform.position + forwardDirection * randomDistance;

            if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, 15.0f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(minDistanceFromPlayer, spawnRadius);
        Vector3 fallbackPos = playerTransform.position + new Vector3(randomCircle.x, 2f, randomCircle.y);

        if (NavMesh.SamplePosition(fallbackPos, out NavMeshHit hitFallback, 15.0f, NavMesh.AllAreas))
        {
            return hitFallback.position;
        }

        return Vector3.zero;
    }
}