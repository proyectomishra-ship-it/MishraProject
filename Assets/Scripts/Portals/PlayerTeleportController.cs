using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Va en los prefabs de jugador (Warrior, Mage, Hunter).
///
/// 1) Cambio de ubicación seguro con los sistemas actuales:
///    - CharacterController: se desactiva un instante para poder setear la posición.
///    - MovementController: se limpia velocidad/dirección residual.
///    - NetworkTransform (server authority): Teleport() evita que los clientes
///      interpolen a través de todo el mapa.
///    - EnemySleepManager: se pide una evaluación inmediata para que los
///      enemigos del destino despierten y los del origen duerman en el acto.
///    - Cinemachine: se avisa a la cámara del dueño para que no haga un barrido largo.
///
/// 2) Registro estático de jugadores spawneados (Active), presente en TODAS las
///    máquinas. Lo usa LocationZone (cliente y servidor) para decidir si hay
///    alguien cerca de una ubicación.
/// </summary>
public class PlayerTeleportController : NetworkBehaviour
{
    public static readonly List<PlayerTeleportController> Active = new();

    private CharacterController characterController;
    private MovementController movementController;
    private NetworkTransform networkTransform;

    // Con "Enter Play Mode Options" sin domain reload, las estáticas sobreviven
    // entre sesiones de Play. Esto las limpia.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Active.Clear();

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        movementController = GetComponent<MovementController>();
        networkTransform = GetComponent<NetworkTransform>();
    }

    public override void OnNetworkSpawn()
    {
        if (!Active.Contains(this)) Active.Add(this);

        // Un jugador nuevo puede aparecer cerca de enemigos dormidos.
        if (IsServer) EnemySleepManager.RequestEvaluation();
    }

    public override void OnNetworkDespawn()
    {
        Active.Remove(this);

        // Si se fue, los enemigos que lo rodeaban pueden volver a dormir.
        if (IsServer) EnemySleepManager.RequestEvaluation();
    }

    public override void OnDestroy()
    {
        Active.Remove(this);
        base.OnDestroy();
    }

    /// <summary>Solo servidor.</summary>
    public void TeleportServer(Vector3 position, Quaternion rotation)
    {
        if (!IsServer) return;

        Vector3 delta = position - transform.position;

        if (movementController != null)
            movementController.ResetMovementState();

        if (characterController != null)
            characterController.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (networkTransform != null)
            networkTransform.Teleport(position, rotation, transform.localScale);

        if (characterController != null)
            characterController.enabled = true;

        // Los enemigos del destino despiertan y los del origen duermen ya,
        // sin esperar al intervalo del manager.
        EnemySleepManager.RequestEvaluation();

        WarpCameraRpc(delta);
    }

    [Rpc(SendTo.Owner)]
    private void WarpCameraRpc(Vector3 positionDelta)
    {
        var vcam = GetComponentInChildren<CinemachineCamera>();
        if (vcam == null || vcam.Follow == null) return;

        vcam.OnTargetObjectWarped(vcam.Follow, positionDelta);
    }
}
