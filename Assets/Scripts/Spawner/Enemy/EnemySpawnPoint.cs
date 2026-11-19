using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// Punto de spawn de enemigos (solo actúa en el servidor).
///
///  - Mantiene hasta N enemigos vivos. N sube con la cantidad de jugadores conectados
///    (ver "Cantidad según jugadores") pero nunca pasa de maxEnemiesPerPoint, y entre
///    todos los puntos nunca hay más de GlobalAliveCap enemigos vivos.
///  - Cada enemigo muerto reaparece pasado respawnDelaySeconds, pero NUNCA mientras haya
///    un jugador a menos de playerClearRadius metros: espera a que se alejen.
///  - El jefe (DemiGod) siempre es uno solo, sin importar la cantidad de jugadores.
/// </summary>
public class EnemySpawnPoint : NetworkBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float respawnDelaySeconds = 7200f;

    [Header("Cantidad según jugadores")]
    [Tooltip("Si está apagado, este punto siempre mantiene 1 enemigo.")]
    [SerializeField] private bool scaleWithPlayers = true;
    [Tooltip("Enemigos extra por cada jugador adicional. 0.5 = uno más cada 2 jugadores extra " +
             "(1-2 jugadores: 1, 3-4: 2, 5-6: 3). 1 = uno más por jugador.")]
    [Min(0f)]
    [SerializeField] private float extraEnemiesPerPlayer = 0.5f;
    [Tooltip("Tope de enemigos vivos de este punto.")]
    [Min(1)]
    [SerializeField] private int maxEnemiesPerPoint = 3;
    [Tooltip("Los enemigos extra aparecen a esta distancia (máx.) alrededor del punto.")]
    [SerializeField] private float extraSpawnRadius = 5f;
    [Tooltip("Segundos mínimos entre un spawn y el siguiente de este punto.")]
    [SerializeField] private float spawnInterval = 3f;

    [Header("Seguridad")]
    [Tooltip("No se spawnea (ni reaparece) nada mientras haya un jugador a menos de esta distancia del punto.")]
    [SerializeField] private float playerClearRadius = 25f;

    // Tope de enemigos vivos entre TODOS los puntos (evita que el mapa se sature).
    private const int GlobalAliveCap = 30;

    // Tiempo maximo de espera al evento de "todos los clientes terminaron de
    // cargar la escena". Netcode lo dispara siempre (aun por timeout), asi que
    // esto es solo una red de seguridad.
    private const float SceneLoadFailsafeSeconds = 60f;

    private const float CheckInterval = 0.5f;
    private const float FailureRetrySeconds = 30f;

    private static readonly List<EnemySpawnPoint> activePoints = new List<EnemySpawnPoint>();

    private readonly List<NetworkObject> alive = new List<NetworkObject>();
    // Momento (ServerTime) en que cada enemigo muerto puede reaparecer. Orden ascendente.
    private readonly List<double> respawnTimes = new List<double>();

    private bool initialSpawnDone;
    private double nextAttemptTime;
    private float checkTimer;
    private NetworkSceneManager subscribedSceneManager;

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[Spawner] OnNetworkSpawn | IsServer: {IsServer}");

        if (!IsServer) return;

        if (!activePoints.Contains(this))
            activePoints.Add(this);

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
        activePoints.Remove(this);
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

        // El primer enemigo aparece siempre en el punto. Si corresponden más
        // (hay varios jugadores) se suman solos desde Update, con las mismas reglas.
        if (!SpawnOne(transform.position))
            nextAttemptTime = NetworkManager.ServerTime.Time + FailureRetrySeconds;
    }

    private void UnsubscribeSceneEvents()
    {
        if (subscribedSceneManager == null) return;

        subscribedSceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
        subscribedSceneManager = null;
    }

    // =========================================================
    // CANTIDAD OBJETIVO
    // =========================================================

    private bool CanScale =>
        scaleWithPlayers &&
        enemyPrefab != null &&
        enemyPrefab.GetComponent<DemiGodAIController>() == null; // el jefe es uno solo

    private int DesiredCount()
    {
        if (!CanScale) return 1;

        int players = Mathf.Max(1, NetworkManager.ConnectedClientsIds.Count);
        int extra = Mathf.FloorToInt((players - 1) * extraEnemiesPerPlayer);

        return Mathf.Clamp(1 + extra, 1, Mathf.Max(1, maxEnemiesPerPoint));
    }

    private static int GlobalAliveCount()
    {
        int total = 0;
        for (int i = 0; i < activePoints.Count; i++)
            if (activePoints[i] != null)
                total += activePoints[i].alive.Count;

        return total;
    }

    // =========================================================
    // LOOP
    // =========================================================

    private void Update()
    {
        if (!IsServer || !initialSpawnDone || enemyPrefab == null) return;

        checkTimer -= Time.unscaledDeltaTime;
        if (checkTimer > 0f) return;
        checkTimer = CheckInterval;

        double now = NetworkManager.ServerTime.Time;

        CleanUpLostEnemies(now);

        int missing = DesiredCount() - alive.Count;

        // Ya hay los que corresponden (o más, si se fue un jugador: los de sobra no se
        // eliminan, simplemente no reaparecen).
        if (missing <= 0)
        {
            respawnTimes.Clear();
            return;
        }

        // Si bajó la cantidad objetivo, sobran temporizadores.
        while (respawnTimes.Count > missing)
            respawnTimes.RemoveAt(respawnTimes.Count - 1);

        if (now < nextAttemptTime) return;

        // Un lugar libre sin temporizador (jugadores nuevos) está listo ya;
        // si no, hay que esperar el temporizador del enemigo muerto.
        bool freeSlot = respawnTimes.Count < missing;
        bool timerReady = respawnTimes.Count > 0 && respawnTimes[0] <= now;

        if (!freeSlot && !timerReady) return;

        // No reaparecer encima de los jugadores ni pasar el tope global.
        if (PlayerNearby()) return;
        if (GlobalAliveCount() >= GlobalAliveCap) return;

        Vector3 position = alive.Count == 0 ? transform.position : RandomNearbyPosition();

        if (SpawnOne(position))
        {
            // Se consume primero el lugar libre; si no, el temporizador cumplido.
            if (!freeSlot && respawnTimes.Count > 0)
                respawnTimes.RemoveAt(0);

            nextAttemptTime = now + spawnInterval;
        }
        else
        {
            nextAttemptTime = now + FailureRetrySeconds;
        }
    }

    /// <summary>Enemigos que desaparecieron sin pasar por Enemy.Die() (despawn externo, destruidos...).</summary>
    private void CleanUpLostEnemies(double now)
    {
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            NetworkObject obj = alive[i];
            if (obj != null && obj.IsSpawned) continue;

            Debug.LogWarning($"[Spawner] {name}: un enemigo desapareció sin evento de muerte. Iniciando cooldown.");
            alive.RemoveAt(i);
            AddRespawnTimer(now);
        }
    }

    private void AddRespawnTimer(double now)
    {
        respawnTimes.Add(now + respawnDelaySeconds);
        respawnTimes.Sort();
    }

    private bool PlayerNearby()
    {
        if (playerClearRadius <= 0f) return false;

        float sqr = playerClearRadius * playerClearRadius;
        Vector3 center = transform.position;

        foreach (var client in NetworkManager.ConnectedClients.Values)
        {
            NetworkObject player = client.PlayerObject;
            if (player == null || !player.IsSpawned) continue;

            if ((player.transform.position - center).sqrMagnitude < sqr)
                return true;
        }

        return false;
    }

    private Vector3 RandomNearbyPosition()
    {
        Vector2 circle = Random.insideUnitCircle * Mathf.Max(0f, extraSpawnRadius);
        Vector3 candidate = transform.position + new Vector3(circle.x, 0f, circle.y);

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            return hit.position;

        return transform.position;
    }

    // =========================================================
    // SPAWN / MUERTE
    // =========================================================

    private bool SpawnOne(Vector3 position)
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("[Spawner] enemyPrefab es NULL");
            return false;
        }

        GameObject enemy = Instantiate(enemyPrefab, position, transform.rotation);

        NetworkObject netObj = enemy.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[Spawner] El prefab no tiene NetworkObject");
            Destroy(enemy);
            return false;
        }

        netObj.Spawn(true);

        alive.Add(netObj);

        Enemy enemyScript = enemy.GetComponent<Enemy>();

        if (enemyScript != null)
        {
            enemyScript.OnEnemyDeath += OnEnemyDeath;
        }
        else
        {
            Debug.LogWarning("[Spawner] Enemy sin script Enemy");
        }

        Debug.Log($"[Spawner] Enemy spawneado en {position} ({alive.Count} vivos en {name}, {GlobalAliveCount()} en total)");
        return true;
    }

    private void OnEnemyDeath(Enemy enemy)
    {
        if (!IsServer || enemy == null) return;

        NetworkObject obj = enemy.GetComponent<NetworkObject>();

        if (!alive.Remove(obj)) return;

        Debug.Log($"[Spawner] Enemy muerto en {name}, iniciando cooldown");

        AddRespawnTimer(NetworkManager.ServerTime.Time);
    }
}
