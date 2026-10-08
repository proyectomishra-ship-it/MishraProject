using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Portal de cambio de ubicación. Se le asigna a cualquier entrada o salida
/// (overworld -> cueva, cueva -> overworld, cueva -> cueva). Todo vive en la
/// misma escena, así que el destino se asigna directo con un Transform.
///
/// Online: SOLO el servidor lo procesa. El movimiento de los jugadores ya es
/// server-authoritative (CharacterController movido en el servidor), así que el
/// trigger se dispara ahí y el teletransporte se replica por NetworkTransform.
///
/// Protecciones para cambios repetidos:
///   - Cooldown por jugador, aplicado ANTES de teletransportar.
///   - Un jugador muerto no usa portales (PlayerRespawnController lo revive en
///     su posición de muerte; teletransportarlo lo dejaría con el respawn en la
///     ubicación equivocada).
///   - Si el destino tiene LocationZone, se la activa ANTES de mover al jugador
///     para que el suelo exista en el servidor.
///   - El teletransporte pide una evaluación inmediata a EnemySleepManager.
///
/// SETUP:
///   1. GameObject con un Collider (se fuerza isTrigger = true).
///   2. Agregar este componente.
///   3. "Destination": Transform vacío donde aparece el jugador (un poco sobre
///      el suelo; su rotación define hacia dónde mira). Debe quedar FUERA del
///      trigger del portal de vuelta.
///   4. "Destination Zone": opcional, solo si el destino tiene una LocationZone
///      para encender/apagar visuales.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ScenePortal : MonoBehaviour
{
    [Header("Destino")]
    [SerializeField] private Transform destination;
    [Tooltip("Opcional. LocationZone del destino que se activa antes de llegar.")]
    [SerializeField] private LocationZone destinationZone;

    [Header("Comportamiento")]
    [Tooltip("Segundos sin poder usar ningún portal tras teletransportarse. Evita rebotes.")]
    [SerializeField] private float cooldownSeconds = 1.5f;

    // clientId -> Time.time hasta el cual está bloqueado
    private static readonly Dictionary<ulong, float> BlockedUntil = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => BlockedUntil.Clear();

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnValidate()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null || !netObj.IsPlayerObject) return;

        if (destination == null)
        {
            Debug.LogError($"[ScenePortal] '{name}' no tiene Destination asignado.", this);
            return;
        }

        var respawn = netObj.GetComponent<PlayerRespawnController>();
        if (respawn != null && respawn.IsDead.Value) return;

        ulong clientId = netObj.OwnerClientId;

        if (BlockedUntil.TryGetValue(clientId, out float until) && Time.time < until)
            return;

        var teleport = netObj.GetComponent<PlayerTeleportController>();
        if (teleport == null)
        {
            Debug.LogError($"[ScenePortal] El jugador {netObj.name} no tiene PlayerTeleportController. " +
                           "Agregalo a los prefabs de Warrior/Mage/Hunter.", this);
            return;
        }

        BlockedUntil[clientId] = Time.time + cooldownSeconds;

        // Primero el destino (suelo/visuales), después el jugador.
        if (destinationZone != null)
            destinationZone.EnsureActive();

        teleport.TeleportServer(destination.position, destination.rotation);

        Debug.Log($"[ScenePortal] Cliente {clientId} -> '{destination.name}'");
    }

    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(0.8f, 0.3f, 1f, 0.5f);
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }

        if (destination != null)
        {
            Gizmos.color = new Color(0.8f, 0.3f, 1f, 0.9f);
            Gizmos.DrawLine(transform.position, destination.position);
            Gizmos.DrawWireSphere(destination.position, 0.4f);
            Gizmos.DrawRay(destination.position, destination.forward * 1.2f);
        }
    }
}
