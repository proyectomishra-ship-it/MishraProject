using System.Collections;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Reemplaza el spawn automático del NetworkManager por uno que
/// elige el prefab correcto según la clase elegida por cada cliente.
///
/// SETUP EN UNITY:
///   - Agregar este script al mismo GameObject que tiene el NetworkManager en Scene1
///   - En el NetworkManager: Default Player Prefab → None
///   - Asignar los tres prefabs de jugador en el Inspector
///   - Crear GameObjects vacíos en la escena como SpawnPoints y asignarlos
/// </summary>
public class ClassAwareNetworkBootstrap : MonoBehaviour
{
    [Header("Prefabs de jugador por clase")]
    [SerializeField] private GameObject warriorPrefab;
    [SerializeField] private GameObject magePrefab;
    [SerializeField] private GameObject hunterPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    private int spawnIndex = 0;

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

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (scene.name != "Scene1") return;

        // Spawnear todos los clientes ya conectados cuando carga Scene1
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            StartCoroutine(SpawnPlayerDelayed(clientId));
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        // Solo spawnear en Scene1, no en CharacterSelect ni NetworkMenu
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene != "Scene1") return;

        StartCoroutine(SpawnPlayerDelayed(clientId));
    }

    private IEnumerator SpawnPlayerDelayed(ulong clientId)
    {
        // Esperar dos frames para que el ServerRpc de clase haya llegado
        yield return null;
        yield return null;
        SpawnPlayer(clientId);
    }

    private void SpawnPlayer(ulong clientId)
    {
        // Obtener clase elegida
        string className = "Warrior";
        if (GameSessionData.Instance != null)
            className = GameSessionData.Instance.GetPlayerClass(clientId);

        // Fallback a PlayerPrefs si no hay sesión
        if (string.IsNullOrEmpty(className))
            className = PlayerPrefs.GetString("SelectedClass", "Warrior");

        GameObject prefab = GetPrefabForClass(className);

        if (prefab == null)
        {
            Debug.LogError($"[ClassSpawn] Prefab null para clase '{className}'");
            return;
        }

        Vector3 spawnPos = GetSpawnPosition();
        Quaternion spawnRot = Quaternion.identity;

        var playerObj = Instantiate(prefab, spawnPos, spawnRot);
        var netObj = playerObj.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[ClassSpawn] El prefab no tiene NetworkObject.");
            Destroy(playerObj);
            return;
        }

        netObj.SpawnAsPlayerObject(clientId, true);
        Debug.Log($"[ClassSpawn] Spawneado '{className}' para clientId {clientId} en {spawnPos}");
    }

    private GameObject GetPrefabForClass(string className) => className switch
    {
        "Warrior" => warriorPrefab,
        "Mage" => magePrefab,
        "Hunter" => hunterPrefab,
        _ => warriorPrefab
    };

    private Vector3 GetSpawnPosition()
    {
        // Primero intentar con los spawn points asignados en el Inspector
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            var pos = spawnPoints[spawnIndex % spawnPoints.Length].position;
            spawnIndex++;
            return pos;
        }

        // Si no hay asignados, buscar por nombre en la escena activa
        var found = new System.Collections.Generic.List<GameObject>();
        for (int i = 1; i <= 10; i++)
        {
            var go = GameObject.Find($"SpawnPoint{i}");
            if (go != null) found.Add(go);
        }

        if (found.Count > 0)
        {
            var pos = found[spawnIndex % found.Count].transform.position;
            spawnIndex++;
            return pos;
        }

        // Fallback: posicion aleatoria sobre el suelo
        return new Vector3(Random.Range(-3f, 3f), 2f, Random.Range(-3f, 3f));
    }
}