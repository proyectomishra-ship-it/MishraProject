using UnityEngine;
using Unity.Netcode;

public class EnemySpawner : NetworkBehaviour
{
    [SerializeField] private GameObject enemyPrefab;

    [SerializeField] private Transform[] spawnPoints;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        SpawnAllEnemies();
    }

    private void SpawnAllEnemies()
    {
        foreach (var point in spawnPoints)
        {
            SpawnEnemy(point.position);
        }
    }

    private void SpawnEnemy(Vector3 position)
    {
        GameObject enemy = Instantiate(enemyPrefab, position, Quaternion.identity);

        NetworkObject netObj = enemy.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[Spawner] Prefab sin NetworkObject");
            return;
        }

        netObj.Spawn(true); // true = visible para todos

        Debug.Log($"[Spawner] Enemy spawneado en {position}");
    }
}