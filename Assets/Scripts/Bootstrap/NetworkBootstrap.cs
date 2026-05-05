using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

/// <summary>
/// Punto de arranque de la sesión de red.
/// También inicializa el ItemDatabase para que todo el sistema de items funcione.
///
/// Asignar el asset ItemDatabase en el Inspector de este componente.
/// </summary>
public class NetworkBootstrap : MonoBehaviour
{
    [Header("Datos globales")]
    [Tooltip("Asset ItemDatabase con todos los items del juego registrados.")]
    [SerializeField] private ItemDatabase itemDatabase;

    private void Start()
    {
        // Inicializar la base de datos de items antes de que cualquier
        // sistema de pickup, inventario o equipamiento intente usarla.
        if (itemDatabase != null)
            itemDatabase.Initialize();
        else
            Debug.LogWarning("[NetBootstrap] ItemDatabase no asignado. " +
                             "El sistema de items no funcionará correctamente.");

        Debug.Log("[NetBootstrap] Script activo. Esperando input...");

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetBootstrap] NetworkManager.Singleton es NULL");
            return;
        }

        NetworkManager.Singleton.OnServerStarted += () =>
        {
            Debug.Log("[Netcode] Server STARTED");
        };

        NetworkManager.Singleton.OnClientConnectedCallback += (clientId) =>
        {
            Debug.Log($"[Netcode] Cliente conectado: {clientId}");
            if (NetworkManager.Singleton.LocalClientId == clientId)
                Debug.Log("[Netcode] Este es el cliente LOCAL");
        };

        NetworkManager.Singleton.OnClientDisconnectCallback += (clientId) =>
        {
            Debug.Log($"[Netcode] Cliente desconectado: {clientId}");
        };
    }

    private void Update()
    {
        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            Debug.Log("[Input] H presionado -> StartHost()");
            NetworkManager.Singleton.StartHost();
        }

        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            Debug.Log("[Input] C presionado -> StartClient()");
            NetworkManager.Singleton.StartClient();
        }
    }
}
