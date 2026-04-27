using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class NetworkBootstrap : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("[NetBootstrap] Script activo. Esperando input...");

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetBootstrap] NetworkManager.Singleton es NULL");
            return;
        }

        // Eventos clave
        NetworkManager.Singleton.OnServerStarted += () =>
        {
            Debug.Log("[Netcode] Server STARTED");
        };

        NetworkManager.Singleton.OnClientConnectedCallback += (clientId) =>
        {
            Debug.Log($"[Netcode] Cliente conectado: {clientId}");

            if (NetworkManager.Singleton.LocalClientId == clientId)
            {
                Debug.Log("[Netcode] Este es el cliente LOCAL");
            }
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
            Debug.Log("[Input] H presionado  StartHost()");
            NetworkManager.Singleton.StartHost();
        }

        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            Debug.Log("[Input] C presionado  StartClient()");
            NetworkManager.Singleton.StartClient();
        }
    }
}