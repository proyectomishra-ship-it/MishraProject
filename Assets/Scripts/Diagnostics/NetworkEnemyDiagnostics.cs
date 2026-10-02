#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// DIAGNOSTICO TEMPORAL. Se crea solo al iniciar el juego (no hay que
/// agregarlo a ninguna escena) y cada pocos segundos imprime en la consola
/// de CADA instancia (host y cliente) que enemigos existen localmente y en
/// que estado estan. Comparando el reporte del host con el del cliente se ve
/// exactamente donde se rompe:
///   - si el cliente no tiene enemigos (no llegaron por red),
///   - si los tiene pero estan inactivos / sin renderers / fuera de camara,
///   - si estan en otra posicion que en el host.
/// Tambien registra los eventos de carga de escena de Netcode, para ver el
/// orden en que el cliente carga la escena y recibe los objetos.
/// Para quitarlo: borrar este archivo.
/// </summary>
public class NetworkEnemyDiagnostics : MonoBehaviour
{
    private const string GameScene = "Scene1";
    private const int MaxEnemiesListed = 12;

    private float timer;
    private float sceneTime;
    private NetworkManager subscribedTo;

    // Objetos de red conocidos en esta maquina (id -> nombre), para detectar
    // cuando uno aparece o desaparece y en que escena estaba.
    private readonly Dictionary<ulong, string> known = new Dictionary<ulong, string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        var go = new GameObject("[NetworkEnemyDiagnostics]");
        DontDestroyOnLoad(go);
        go.AddComponent<NetworkEnemyDiagnostics>();
    }

    private void Update()
    {
        NetworkManager nm = NetworkManager.Singleton;

        if (nm == null || !nm.IsListening)
        {
            subscribedTo = null;
            known.Clear();
            return;
        }

        SubscribeSceneEvents(nm);
        TrackSpawnedObjects(nm);

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != GameScene)
        {
            sceneTime = 0f;
            return;
        }

        sceneTime += Time.unscaledDeltaTime;
        timer += Time.unscaledDeltaTime;

        // Cada 3 s durante el primer minuto en la escena, despues cada 15 s.
        float interval = sceneTime < 60f ? 3f : 15f;
        if (timer < interval) return;

        timer = 0f;
        Report(nm);
    }

    // ---------------------------------------------------------------------

    private static string Role(NetworkManager nm) =>
        nm.IsHost ? "HOST" : nm.IsServer ? "SERVER" : "CLIENT";

    private void SubscribeSceneEvents(NetworkManager nm)
    {
        if (subscribedTo == nm || nm.SceneManager == null) return;

        subscribedTo = nm;

        // Hace que el propio Netcode escriba mas detalle sobre spawns y
        // problemas de escena en la consola.
        nm.LogLevel = LogLevel.Developer;

        nm.SceneManager.OnSceneEvent += e =>
            Debug.Log($"[Diag:{Role(nm)}] SceneEvent {e.SceneEventType} " +
                      $"escena='{e.SceneName}' clientId={e.ClientId} t={Time.realtimeSinceStartup:F1}s");
    }

    private void TrackSpawnedObjects(NetworkManager nm)
    {
        if (nm.SpawnManager == null) return;

        var current = nm.SpawnManager.SpawnedObjects;
        string active = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Objetos nuevos
        foreach (var kv in current)
        {
            if (known.ContainsKey(kv.Key)) continue;

            string objName = kv.Value != null ? kv.Value.name : "(null)";
            string scene = kv.Value != null ? kv.Value.gameObject.scene.name : "?";
            known[kv.Key] = objName;

            Debug.Log($"[Diag:{Role(nm)}] + SPAWN id={kv.Key} '{objName}' " +
                      $"escena='{scene}' escenaActiva='{active}' t={Time.realtimeSinceStartup:F1}s");
        }

        // Objetos que desaparecieron
        List<ulong> gone = null;
        foreach (var kv in known)
        {
            if (current.ContainsKey(kv.Key)) continue;
            (gone ??= new List<ulong>()).Add(kv.Key);
        }

        if (gone == null) return;

        foreach (ulong id in gone)
        {
            Debug.Log($"[Diag:{Role(nm)}] - DESPAWN id={id} '{known[id]}' " +
                      $"escenaActiva='{active}' t={Time.realtimeSinceStartup:F1}s");
            known.Remove(id);
        }
    }

    private void Report(NetworkManager nm)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[Diag:{Role(nm)}] ===== Reporte de enemigos (t={Time.realtimeSinceStartup:F1}s) =====");

        // 1) Puntos de spawn de escena: si en el cliente no estan "spawned",
        //    los objetos de escena no se sincronizaron.
        var spawnPoints = FindObjectsByType<EnemySpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int spSpawned = 0;
        foreach (var sp in spawnPoints) if (sp.IsSpawned) spSpawned++;
        sb.AppendLine($"  EnemySpawnPoint: total={spawnPoints.Length}, spawned={spSpawned}");

        // 2) NetworkObjects que Netcode considera spawneados en esta maquina.
        int netTotal = nm.SpawnManager.SpawnedObjects.Count;
        int netEnemies = 0;
        foreach (var kv in nm.SpawnManager.SpawnedObjects)
            if (kv.Value != null && kv.Value.GetComponent<Enemy>() != null) netEnemies++;
        sb.AppendLine($"  NetworkObjects spawneados={netTotal}, de los cuales enemigos={netEnemies}");

        // 3) Enemigos presentes en la escena local.
        var enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"  Enemy en escena local={enemies.Length}");

        Vector3? playerPos = null;
        var playerObj = nm.LocalClient?.PlayerObject;
        if (playerObj != null) playerPos = playerObj.transform.position;
        sb.AppendLine($"  Jugador local en {(playerPos.HasValue ? playerPos.Value.ToString("F1") : "(sin PlayerObject)")}");

        Camera cam = Camera.main;
        Plane[] planes = cam != null ? GeometryUtility.CalculateFrustumPlanes(cam) : null;
        if (cam == null) sb.AppendLine("  ! Camera.main es NULL");

        int shown = 0;
        foreach (var enemy in enemies)
        {
            if (shown++ >= MaxEnemiesListed) { sb.AppendLine("  ..."); break; }

            var no = enemy.GetComponent<NetworkObject>();
            var renderers = enemy.GetComponentsInChildren<Renderer>(true);

            int enabledCount = 0, visibleCount = 0, inFrustum = 0;
            foreach (var r in renderers)
            {
                if (r.enabled && r.gameObject.activeInHierarchy) enabledCount++;
                if (r.isVisible) visibleCount++;
                if (planes != null && r.enabled && GeometryUtility.TestPlanesAABB(planes, r.bounds)) inFrustum++;
            }

            var agent = enemy.GetComponent<NavMeshAgent>();
            string dist = playerPos.HasValue
                ? Vector3.Distance(playerPos.Value, enemy.transform.position).ToString("F1") + "m"
                : "?";

            sb.AppendLine(
                $"  - {enemy.name} netId={(no != null ? no.NetworkObjectId.ToString() : "?")} " +
                $"spawned={(no != null && no.IsSpawned)} activo={enemy.gameObject.activeInHierarchy} " +
                $"pos={enemy.transform.position.ToString("F1")} dist={dist} layer={enemy.gameObject.layer} " +
                $"renderers={renderers.Length} (habilitados={enabledCount}, enFrustum={inFrustum}, isVisible={visibleCount}) " +
                $"agente={(agent == null ? "no" : agent.enabled + "/enNavMesh=" + agent.isOnNavMesh)}");
        }

        Debug.Log(sb.ToString());
    }
}
#endif