using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class EnemySpawnPoint : NetworkBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float respawnDelaySeconds = 7200f;

    // Tiempo maximo de espera al evento de "todos los clientes terminaron de
    // cargar la escena". Netcode lo dispara siempre (aun por timeout), asi que
    // esto es solo una red de seguridad.
    private const float SceneLoadFailsafeSeconds = 60f;

    private NetworkObject currentEnemy;
    private double deathTime = -1;
    private bool initialSpawnDone;
    private NetworkSceneManager subscribedSceneManager;

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[Spawner] OnNetworkSpawn | IsServer: {IsServer}");

        if (!IsServer) return;

        // Los enemigos NO se pueden spawnear aca si hay clientes remotos:
        // este metodo corre mientras el servidor termina de cargar la escena,
        // y los clientes todavia estan en plena transicion (aun en la escena
        // anterior). El CreateObject les llegaba en ese momento, el enemigo se
        // creaba en la escena vieja y se destruia al descargarla. Resultado:
        // existian en el servidor pero el cliente nunca los veia.
        if (!HasRemoteClients())
        {
            // Sin clientes remotos no hay carrera posible: comportamiento de siempre.
            DoInitialSpawn();
            return;
        }

        // Con clientes remotos, esperar a que Netcode avise que TODOS terminaron
        // de cargar la escena.
        subscribedSceneManager = NetworkManager.SceneManager;
        if (subscribedSceneManager != null)
            subscribedSceneManager.OnLoadEventCompleted += OnLoadEventCompleted;

        StartCoroutine(InitialSpawnFailsafe());
    }

    public override void OnNetworkDespawn()
    {
        UnsubscribeSceneEvents();
    }

    private bool HasRemoteClients()
    {
        foreach (ulong id in NetworkManager.ConnectedClientsIds)
            if (id != NetworkManager.ServerClientId)
                return true;

        return false;
    }

    private void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode,
                                      List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        // El spawn point pudo destruirse (cambio de escena) con el evento aun suscrito.
        if (this == null) return;

        // Solo la carga de la escena a la que pertenece este spawn point.
        if (sceneName != gameObject.scene.name) return;

        Debug.Log($"[Spawner] Todos los clientes cargaron '{sceneName}'. Spawneando enemigo en {name}.");
        DoInitialSpawn();
    }

    private IEnumerator InitialSpawnFailsafe()
    {
        yield return new WaitForSecondsRealtime(SceneLoadFailsafeSeconds);

        if (initialSpawnDone) yield break;

        Debug.LogWarning($"[Spawner] {name}: no llego el evento de carga de escena en " +
                         $"{SceneLoadFailsafeSeconds}s. Spawneando de todas formas.");
        DoInitialSpawn();
    }

    private void DoInitialSpawn()
    {
        if (initialSpawnDone) return;

        initialSpawnDone = true;
        UnsubscribeSceneEvents();
        TrySpawn();
    }

    private void UnsubscribeSceneEvents()
    {
        if (subscribedSceneManager == null) return;

        subscribedSceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
        subscribedSceneManager = null;
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