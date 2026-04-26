using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class PlayerCameraBinder : NetworkBehaviour
{
    private CinemachineCamera cam;
    private Transform cameraPivot;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        Debug.Log("[Camera] Binding cámara local");

  
        cam = Object.FindFirstObjectByType<CinemachineCamera>();

        if (cam == null)
        {
            Debug.LogError("[Camera] No se encontró CinemachineCamera en la escena");
            return;
        }

      
        cameraPivot = transform.Find("CameraPivot");

        if (cameraPivot == null)
        {
            Debug.LogError("[Camera] No existe 'CameraPivot' como hijo del Player");

       
            cameraPivot = transform;
        }

     
        cam.Follow = cameraPivot;
        cam.LookAt = cameraPivot;

        Debug.Log($"[Camera] Follow asignado a: {cameraPivot.name}");
    }
}