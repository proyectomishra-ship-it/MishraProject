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

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;
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

        Vector3    spawnPos = GetSpawnPosition();
        Quaternion spawnRot = Quaternion.identity;

        var playerObj = Instantiate(prefab, spawnPos, spawnRot);
        var netObj    = playerObj.GetComponent<NetworkObject>();

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
        "Mage"    => magePrefab,
        "Hunter"  => hunterPrefab,
        _         => warriorPrefab
    };

    private Vector3 GetSpawnPosition()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            var pos = spawnPoints[spawnIndex % spawnPoints.Length].position;
            spawnIndex++;
            return pos;
        }
        return new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
    }
}
