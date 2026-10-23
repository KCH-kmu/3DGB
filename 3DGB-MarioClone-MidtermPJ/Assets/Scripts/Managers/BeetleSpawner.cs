using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeetleSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private BeetleMovement beetlePrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField, Min(1)] private int maxBeetles = 3;
    [SerializeField, Min(0f)] private float respawnDelay = 2f;
    [SerializeField, Min(0f)] private float spawnClearRadius = 0.75f;

    private readonly List<BeetleMovement> activeBeetles =
        new List<BeetleMovement>();

    private Coroutine respawnCoroutine;
    private bool hasShownSetupWarning;

    private void Start()
    {
        // 씬에 미리 배치된 비틀도 전체 개체 수에 포함한다.
        activeBeetles.AddRange(
            FindObjectsOfType<BeetleMovement>()
        );

        FillMissingBeetles();
    }

    private void Update()
    {
        RemoveDestroyedBeetles();

        if (
            activeBeetles.Count < maxBeetles &&
            respawnCoroutine == null
        )
        {
            respawnCoroutine =
                StartCoroutine(RespawnMissingBeetles());
        }
    }

    private IEnumerator RespawnMissingBeetles()
    {
        yield return new WaitForSeconds(respawnDelay);

        RemoveDestroyedBeetles();
        FillMissingBeetles();

        respawnCoroutine = null;
    }

    private void FillMissingBeetles()
    {
        if (!HasValidSetup())
        {
            return;
        }

        List<Transform> freeSpawnPoints = GetFreeSpawnPoints();

        while (
            activeBeetles.Count < maxBeetles &&
            freeSpawnPoints.Count > 0
        )
        {
            int randomIndex = Random.Range(
                0,
                freeSpawnPoints.Count
            );

            SpawnBeetle(freeSpawnPoints[randomIndex]);
            freeSpawnPoints.RemoveAt(randomIndex);
        }
    }

    private void SpawnBeetle(Transform spawnPoint)
    {
        BeetleMovement newBeetle = Instantiate(
            beetlePrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        activeBeetles.Add(newBeetle);
    }

    private List<Transform> GetFreeSpawnPoints()
    {
        List<Transform> freeSpawnPoints = new List<Transform>();

        foreach (Transform spawnPoint in spawnPoints)
        {
            if (
                spawnPoint != null &&
                IsSpawnPointFree(spawnPoint.position)
            )
            {
                freeSpawnPoints.Add(spawnPoint);
            }
        }

        return freeSpawnPoints;
    }

    private bool IsSpawnPointFree(Vector2 spawnPosition)
    {
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(
            spawnPosition,
            spawnClearRadius
        );

        foreach (Collider2D nearbyCollider in nearbyColliders)
        {
            if (
                nearbyCollider.GetComponentInParent<BeetleMovement>()
                != null
            )
            {
                return false;
            }
        }

        return true;
    }

    private void RemoveDestroyedBeetles()
    {
        activeBeetles.RemoveAll(beetle => beetle == null);
    }

    private bool HasValidSetup()
    {
        bool hasValidSetup =
            beetlePrefab != null &&
            spawnPoints != null &&
            spawnPoints.Length > 0;

        if (!hasValidSetup && !hasShownSetupWarning)
        {
            Debug.LogWarning(
                "BeetleSpawner: Assign a Beetle Prefab and at least one Spawn Point.",
                this
            );

            hasShownSetupWarning = true;
        }

        return hasValidSetup;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        respawnCoroutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;

        foreach (Transform spawnPoint in spawnPoints)
        {
            if (spawnPoint != null)
            {
                Gizmos.DrawWireSphere(
                    spawnPoint.position,
                    spawnClearRadius
                );
            }
        }
    }
}
