using System.Collections;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Reemplaza el spawn automático del NetworkManager por uno que
/// elige el prefab correcto según la clase elegida por cada cliente.
///
/// SETUP EN UNITY:
///   - Este script vive en el mismo GameObject que el NetworkManager (escena NetworkMenu)
///   - En el NetworkManager: Default Player Prefab → None
///   - Asignar los tres prefabs de jugador en el Inspector (acá abajo)
///   - Los SpawnPoints se configuran en Scene1 mediante SpawnPointRegistry
///     (NO acá, ya que el NetworkManager está en otra escena)
/// </summary>
public class ClassAwareNetworkBootstrap : MonoBehaviour
{
    [Header("Prefabs de jugador por clase")]
    [SerializeField] private GameObject warriorPrefab;
    [SerializeField] private GameObject magePrefab;
    [SerializeField] private GameObject hunterPrefab;

    // -------------------------------------------------------
    // NOTA: El array "Spawn Points" fue eliminado intencionalmente.
    //
    // PROBLEMA ORIGINAL:
    //   [SerializeField] private Transform[] spawnPoints;
    //   Este campo estaba en el Inspector del NetworkManager (escena NetworkMenu).
    //   Como los SpawnPoints viven en Scene1 (escena diferente), Unity no puede
    //   mantener referencias cross-scene serializadas. El array quedaba con
    //   Length > 0 pero con referencias null, causando:
    //   → UnassignedReferenceException en spawnPoints[index].position
    //
    // SOLUCIÓN:
    //   Los spawn points ahora se gestionan mediante SpawnPointRegistry,
    //   un componente que vive en Scene1 y puede ser asignado normalmente
    //   en el Inspector porque está en la misma escena que los puntos.
    // -------------------------------------------------------

    private void Start()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // -------------------------------------------------------
    // Eventos de escena y conexión
    // -------------------------------------------------------

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (scene.name != "Scene1") return;

        // Scene1 terminó de cargar → spawnear todos los clientes ya conectados.
        // SpawnPointRegistry ya estará disponible en este punto porque Awake()
        // de los objetos de la escena corre antes de que sceneLoaded dispare.
        Debug.Log($"[ClassSpawn] Scene1 cargada. Spawneando {NetworkManager.Singleton.ConnectedClientsIds.Count} cliente(s).");

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            StartCoroutine(SpawnPlayerDelayed(clientId));
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene != "Scene1") return;

        StartCoroutine(SpawnPlayerDelayed(clientId));
    }

    // -------------------------------------------------------
    // Spawn
    // -------------------------------------------------------

    private IEnumerator SpawnPlayerDelayed(ulong clientId)
    {
        // Esperar dos frames para que el ServerRpc de clase haya llegado
        // y para que SpawnPointRegistry esté completamente inicializado.
        yield return null;
        yield return null;
        SpawnPlayer(clientId);
    }

    private void SpawnPlayer(ulong clientId)
    {
        string className = ResolveClassName(clientId);
        GameObject prefab = GetPrefabForClass(className);

        if (prefab == null)
        {
            Debug.LogError($"[ClassSpawn] Prefab null para clase '{className}'. " +
                           $"Verificá las asignaciones en el Inspector de ClassAwareNetworkBootstrap.");
            return;
        }

        Vector3 spawnPos = GetSpawnPosition();

        var playerObj = Instantiate(prefab, spawnPos, Quaternion.identity);
        var netObj = playerObj.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[ClassSpawn] El prefab no tiene componente NetworkObject.");
            Destroy(playerObj);
            return;
        }

        netObj.SpawnAsPlayerObject(clientId, true);
        Debug.Log($"[ClassSpawn] '{className}' spawneado para clientId {clientId} en {spawnPos}");
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    private string ResolveClassName(ulong clientId)
    {
        if (GameSessionData.Instance != null)
        {
            string cls = GameSessionData.Instance.GetPlayerClass(clientId);
            if (!string.IsNullOrEmpty(cls)) return cls;
        }

        // Fallback a PlayerPrefs (host jugando solo o sesión sin GameSessionData)
        return PlayerPrefs.GetString("SelectedClass", "Warrior");
    }

    private GameObject GetPrefabForClass(string className) => className switch
    {
        "Warrior" => warriorPrefab,
        "Mage" => magePrefab,
        "Hunter" => hunterPrefab,
        _ => warriorPrefab
    };

    /// <summary>
    /// Obtiene la próxima posición de spawn delegando en SpawnPointRegistry.
    /// SpawnPointRegistry vive en Scene1 y maneja su propio índice round-robin.
    /// </summary>
    private Vector3 GetSpawnPosition()
    {
        if (SpawnPointRegistry.Instance != null)
            return SpawnPointRegistry.Instance.GetNextPosition();

        // SpawnPointRegistry no está presente — Scene1 no lo tiene configurado.
        Debug.LogError("[ClassSpawn] SpawnPointRegistry.Instance es null. " +
                       "Agregá el componente SpawnPointRegistry a un GameObject en Scene1 " +
                       "y asigná los spawn points en su Inspector.");

        return new Vector3(Random.Range(-5f, 5f), 1f, Random.Range(-5f, 5f));
    }
}