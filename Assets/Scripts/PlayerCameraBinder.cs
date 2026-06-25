using System.Collections;
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
///   - IsOwner  → espera un frame, apunta la CinemachineCamera al CameraPivot y la activa
///   - !IsOwner → desactiva la CinemachineCamera para no interferir con la cámara local
///
/// POR QUÉ EL FRAME DE ESPERA:
///   OnNetworkSpawn() corre antes de que el NetworkObject termine de posicionarse.
///   Si configurás el Follow en ese mismo frame, Cinemachine puede leer una posición
///   incorrecta (0,0,0 o el origen de la escena) y la cámara "vuela" o no arranca.
///   Esperar un frame garantiza que el transform ya está en el spawn point correcto.
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
                           "Agregá una al prefab y asignala en el Inspector de PlayerCameraBinder.");
            return;
        }

        if (!IsOwner)
        {
            // Jugador remoto: desactivar su cámara virtual para que no compita
            // con la cámara del jugador local.
            virtualCamera.gameObject.SetActive(false);
            return;
        }

        // Jugador local: configurar la cámara en el próximo frame
        // para que el transform ya esté en el spawn point correcto.
        StartCoroutine(ActivateCameraNextFrame());
    }

    private IEnumerator ActivateCameraNextFrame()
    {
        // Esperar un frame — en este punto el NetworkObject ya fue posicionado
        // en el spawn point por ClassAwareNetworkBootstrap.
        yield return null;

        // Buscar el pivot (punto de seguimiento de la cámara)
        Transform pivot = transform.Find(cameraPivotName);
        if (pivot == null)
        {
            Debug.LogWarning($"[Camera] No se encontró '{cameraPivotName}' como hijo del Player. " +
                             "Usando el transform raíz como fallback. " +
                             "Creá un hijo vacío llamado 'CameraPivot' a la altura de los ojos.");
            pivot = transform;
        }

        // Asignar target y activar
        virtualCamera.Follow = pivot;
        virtualCamera.LookAt = pivot;
        virtualCamera.gameObject.SetActive(true);

        Debug.Log($"[Camera] Cámara local activada. Follow → '{pivot.name}' en {pivot.position}");
    }
}