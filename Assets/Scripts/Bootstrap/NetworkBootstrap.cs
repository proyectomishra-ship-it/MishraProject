using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Punto de arranque de la sesión de red.
/// Inicializa el ItemDatabase. La conexión ahora la maneja NetworkMenuUI.
/// </summary>
public class NetworkBootstrap : MonoBehaviour
{
    [Header("Datos globales")]
    [SerializeField] private ItemDatabase itemDatabase;

    private void Awake()
    {
        // Inicializar en Awake (no en Start) para que ItemDatabase.Instance
        // esté disponible antes de cualquier OnNetworkSpawn().
        if (itemDatabase != null)
            itemDatabase.Initialize();
        else
            Debug.LogError("[NetBootstrap] ItemDatabase no asignado en el Inspector.");
    }

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetBootstrap] NetworkManager.Singleton es NULL");
            return;
        }

        NetworkManager.Singleton.OnServerStarted += () =>
            Debug.Log("[Netcode] Server STARTED");

        NetworkManager.Singleton.OnClientConnectedCallback += (clientId) =>
            Debug.Log($"[Netcode] Cliente conectado: {clientId}");

        NetworkManager.Singleton.OnClientDisconnectCallback += (clientId) =>
            Debug.Log($"[Netcode] Cliente desconectado: {clientId}");

        Debug.Log("[NetBootstrap] Listo. Esperando acción del menú...");
    }
}