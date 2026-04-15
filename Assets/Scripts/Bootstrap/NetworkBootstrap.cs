using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class NetworkBootstrap : MonoBehaviour
{
    private void Start()
    {
    }

    private void Update()
    {
        if(Keyboard.current.hKey.wasPressedThisFrame)
        {
            // Inicia como Host (servidor + cliente local) para pruebas.
            NetworkManager.Singleton.StartHost();
        }

        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            // Inicia como cliente para pruebas.
            NetworkManager.Singleton.StartClient();
        }
    }
}