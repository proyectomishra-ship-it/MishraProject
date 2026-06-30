using System.Collections;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Gestiona la cámara Cinemachine para el jugador local.
///
/// SETUP DEL PREFAB:
///   - Agregá un GameObject hijo llamado "CameraPivot" al prefab del Player
///   - Agregá un CinemachineCamera como hijo del Player prefab
///   - Dejá el CinemachineBrain en la Main Camera de la escena (no se toca)
///
/// COMPORTAMIENTO:
///   - IsOwner  → espera a que el NetworkTransform sincronice el spawn point,
///               luego apunta la CinemachineCamera al CameraPivot y la activa.
///   - !IsOwner → desactiva la CinemachineCamera para no interferir con la cámara local.
///
/// POR QUÉ NO ALCANZA CON 1 FRAME (fix para Relay):
///   En LAN/host, el NetworkTransform sincroniza la posición de spawn en el mismo
///   frame que el spawn (misma máquina). 1 frame de espera era suficiente.
///
///   En Relay, la posición de spawn viaja como mensaje de red separado con una
///   latencia de 50–300 ms. En ese tiempo el transform queda en (0, 0, 0) —
///   dentro del terreno. La cámara se activaba mirando hacia adentro del suelo
///   → pantalla negra hasta que NetworkTransform por fin sincronizaba y el
///   jugador saltaba al spawn point real.
///
///   La solución: esperar activamente hasta que transform.position sea distinto
///   del origen, con un timeout de seguridad.
/// </summary>
public class PlayerCameraBinder : NetworkBehaviour
{
    [Tooltip("Si está vacío, se busca automáticamente en los hijos del prefab.")]
    [SerializeField] private CinemachineCamera virtualCamera;

    [Tooltip("Nombre del hijo que usará como Follow/LookAt target.")]
    [SerializeField] private string cameraPivotName = "CameraPivot";

    // Referencia al controlador de input de cámara (mouse look)
    private CinemachineInputAxisController _inputAxisController;

    // Posición en la que el prefab fue instanciado ANTES de que NetworkTransform
    // sincronice el spawn point real. Se usa como centinela: mientras el transform
    // esté ahí, el sync todavía no llegó.
    private Vector3 _initialLocalPos;

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

        // Guardar la posición inicial para detectar cuándo NetworkTransform sincronizó.
        // En el momento del spawn, el objeto puede estar en (0,0,0) o en el origen
        // de la escena hasta que el primer paquete de posición llegue del servidor.
        _initialLocalPos = transform.position;

        StartCoroutine(ActivateCameraWhenReady());
    }

    private IEnumerator ActivateCameraWhenReady()
    {
        // ── Paso 1: esperar al menos 1 frame para que NGO termine el spawn ────
        yield return null;

        // ── Paso 2 (FIX Relay): esperar hasta que el NetworkTransform haya
        //    sincronizado la posición de spawn real.
        //
        //    En LAN/host: la posición ya es correcta en el frame siguiente (no hay
        //    latencia de red), el while termina de inmediato.
        //
        //    En Relay: el transform arranca en _initialLocalPos (normalmente cerca
        //    del origen) y el servidor manda la posición correcta como paquete
        //    separado. Esperamos hasta recibirlo.
        //
        //    Timeout de 5 s: si el servidor no manda la posición en ese tiempo
        //    (conexión muy mala o SpawnPointRegistry en el origen exacto),
        //    activamos la cámara igual para no bloquear al jugador.
        if (!IsServer) // Solo los clientes puros tienen este problema
        {
            const float kTimeout = 5f;
            float elapsed = 0f;

            while (Vector3.Distance(transform.position, _initialLocalPos) < 0.05f
                   && elapsed < kTimeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (elapsed >= kTimeout)
                Debug.LogWarning("[Camera] Timeout esperando sync de posición de spawn. " +
                                 "Activando cámara en posición actual. " +
                                 "Verificá la latencia de Relay y SpawnPointRegistry.");
            else
                Debug.Log($"[Camera] Posición sincronizada en {elapsed:F2}s → {transform.position}");
        }

        // ── Paso 3: configurar y activar la cámara ────────────────────────────
        Transform pivot = transform.Find(cameraPivotName);
        if (pivot == null)
        {
            Debug.LogWarning($"[Camera] No se encontró '{cameraPivotName}' como hijo del Player. " +
                             "Usando el transform raíz como fallback. " +
                             "Creá un hijo vacío llamado 'CameraPivot' a la altura de los ojos.");
            pivot = transform;
        }

        virtualCamera.Follow = pivot;
        virtualCamera.LookAt = pivot;
        virtualCamera.gameObject.SetActive(true);

        _inputAxisController = virtualCamera.GetComponent<CinemachineInputAxisController>();

        Debug.Log($"[Camera] Cámara local activada. Follow → '{pivot.name}' en {pivot.position}");
    }

    /// <summary>
    /// Habilita o deshabilita el mouse look de la cámara.
    /// Llamar con false al abrir el inventario, con true al cerrarlo.
    /// </summary>
    public void SetLookEnabled(bool enabled)
    {
        if (_inputAxisController != null)
            _inputAxisController.enabled = enabled;
    }
}