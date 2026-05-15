using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Gestiona la cámara Cinemachine para el jugador local.
///
/// SETUP DEL PREFAB:
///   - Agregá un GameObject hijo llamado "CameraPivot" al prefab del Player
///   - Agregá un CinemachineCamera como hijo del Player prefab (puede ir dentro de CameraPivot)
///   - Dejá el CinemachineBrain en la Main Camera de la escena (no se toca)
///
/// COMPORTAMIENTO:
///   - IsOwner  → activa la CinemachineCamera y la apunta al CameraPivot
///   - !IsOwner → desactiva la CinemachineCamera para no interferir con otros jugadores
/// </summary>
public class PlayerCameraBinder : NetworkBehaviour
{
    [Tooltip("Si está vacío, se busca automáticamente en los hijos del prefab.")]
    [SerializeField] private CinemachineCamera virtualCamera;

    [Tooltip("Nombre del hijo que usará como Follow/LookAt target.")]
    [SerializeField] private string cameraPivotName = "CameraPivot";

    public override void OnNetworkSpawn()
    {
        // Buscar la virtual cam en los hijos si no está asignada en el Inspector
        if (virtualCamera == null)
            virtualCamera = GetComponentInChildren<CinemachineCamera>(includeInactive: true);

        if (virtualCamera == null)
        {
            Debug.LogError("[Camera] No se encontró CinemachineCamera en los hijos del Player. " +
                           "Agregá una al prefab.");
            return;
        }

        if (!IsOwner)
        {
            // Desactivar la cámara para jugadores remotos — no deben interferir
            virtualCamera.gameObject.SetActive(false);
            Debug.Log($"[Camera] VirtualCam desactivada para jugador remoto: {gameObject.name}");
            return;
        }

        // === JUGADOR LOCAL ===

        // Buscar el pivot
        Transform pivot = transform.Find(cameraPivotName);
        if (pivot == null)
        {
            Debug.LogWarning($"[Camera] No se encontró '{cameraPivotName}' como hijo. " +
                             "Usando el transform del Player como fallback.");
            pivot = transform;
        }

        virtualCamera.gameObject.SetActive(true);
        virtualCamera.Follow = pivot;
        virtualCamera.LookAt = pivot;

        Debug.Log($"[Camera] VirtualCam activa. Follow → '{pivot.name}'");
    }
}