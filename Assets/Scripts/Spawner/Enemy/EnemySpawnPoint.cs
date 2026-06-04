using UnityEngine;
using Unity.Netcode;

public class EnemySpawnPoint : NetworkBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float respawnDelaySeconds = 7200f;

    private NetworkObject currentEnemy;
    private double deathTime = -1;

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[Spawner] OnNetworkSpawn | IsServer: {IsServer}");

        if (!IsServer) return;

        TrySpawn();
    }

    private void Update()
    {
        if (!IsServer) return;

        if (currentEnemy == null)
        {
            if (deathTime < 0) return;

            double elapsed = NetworkManager.ServerTime.Time - deathTime;

            if (elapsed >= respawnDelaySeconds)
            {
                Debug.Log("[Spawner] Respawn listo");
                TrySpawn();
            }
        }
    }

    private void TrySpawn()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("[Spawner] enemyPrefab es NULL");
            return;
        }

        GameObject enemy = Instantiate(enemyPrefab, transform.position, transform.rotation);

        NetworkObject netObj = enemy.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[Spawner] El prefab no tiene NetworkObject");
            return;
        }

        netObj.Spawn(true);

        currentEnemy = netObj;
        deathTime = -1;

        Enemy enemyScript = enemy.GetComponent<Enemy>();

        if (enemyScript != null)
        {
            enemyScript.OnEnemyDeath += OnEnemyDeath;
        }
        else
        {
            Debug.LogWarning("[Spawner] Enemy sin script Enemy");
        }

        Debug.Log($"[Spawner] Enemy spawneado en {transform.position}");
    }

    private void OnEnemyDeath(Enemy enemy)
    {
        if (!IsServer) return;

        Debug.Log("[Spawner] Enemy muerto, iniciando cooldown");

        deathTime = NetworkManager.ServerTime.Time;
        currentEnemy = null;
    }
}