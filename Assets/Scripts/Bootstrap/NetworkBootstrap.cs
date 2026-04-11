using UnityEngine;
using Unity.Netcode;

public class NetworkBootstrap : MonoBehaviour
{
    private void Start()
    {
        // Inicia como Host (servidor + cliente local) para pruebas.
        NetworkManager.Singleton.StartHost();
    }
}