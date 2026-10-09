using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Portal de cambio de ubicación. Es el componente del prefab "Portal".
/// Se usa igual para una entrada (overworld -> cueva), una salida (cueva -> overworld)
/// o cualquier otro par de puntos del mapa.
///
/// USO (lo único que hay que configurar):
///   1. Poner dos instancias del prefab Portal (por ejemplo la entrada y la salida de una cueva).
///   2. En una de ellas, arrastrar la otra al campo "Linked Portal".
///      El otro lado se vincula solo (vínculo bidireccional), no hace falta tocarlo.
///   3. Rotar cada portal para que su eje Z (flecha azul) apunte hacia donde
///      debe salir el jugador cuando LLEGA a ese portal.
///
/// Todo lo demás es automático:
///   - Punto de llegada: el hijo "Arrival" del portal de destino (o, si falta, 3 m
///     delante del portal).
///   - Si el portal de destino está dentro de una cueva con LocationZone, se la
///     activa ANTES de mover al jugador (para que el suelo exista).
///   - Cooldown por jugador, jugador muerto ignorado, evaluación inmediata de
///     enemigos (EnemySleepManager) al llegar.
///
/// Online: SOLO el servidor lo procesa. El movimiento de los jugadores ya es
/// server-authoritative, así que el trigger se dispara ahí y el teletransporte se
/// replica por NetworkTransform.
///
/// IMPORTANTE: el portal no debe estar dentro de un objeto que LocationZone apague
/// (por ejemplo "Visuals"): el trigger tiene que estar siempre activo.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ScenePortal : MonoBehaviour
{
    [Header("Vínculo")]
    [Tooltip("Portal con el que se conecta. Arrastrá el otro portal acá; el otro lado se vincula solo.")]
    [SerializeField] private ScenePortal linkedPortal;
    [Tooltip("Si está activo, el portal de destino NO vuelve a este (portal de un solo sentido).")]
    [SerializeField] private bool oneWay;

    [Header("Llegada a ESTE portal")]
    [Tooltip("Dónde aparece el jugador que llega a este portal. Si está vacío, 3 m delante del portal (eje Z).")]
    [SerializeField] private Transform arrivalPoint;
    [SerializeField] private float fallbackArrivalDistance = 3f;

    [Header("Comportamiento")]
    [Tooltip("Segundos sin poder usar ningún portal tras teletransportarse. Evita rebotes.")]
    [SerializeField] private float cooldownSeconds = 1.5f;

    [Header("Avanzado (normalmente vacío)")]
    [Tooltip("Si se asigna, tiene prioridad sobre el vínculo: lleva a este Transform.")]
    [SerializeField] private Transform destinationOverride;
    [Tooltip("Zona a activar antes de llegar al destino manual (opcional).")]
    [SerializeField] private LocationZone destinationZoneOverride;

    public ScenePortal LinkedPortal => linkedPortal;
    public bool HasManualDestination => destinationOverride != null;

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

    private void Awake()
    {
        // Vínculo bidireccional automático: si el otro lado no apunta a nadie, apunta a este.
        if (linkedPortal != null && !oneWay && linkedPortal.linkedPortal == null)
            linkedPortal.linkedPortal = this;
    }

    /// <summary>Posición y rotación donde aparece un jugador que llega a ESTE portal.</summary>
    public void GetArrival(out Vector3 position, out Quaternion rotation)
    {
        if (arrivalPoint != null)
        {
            position = arrivalPoint.position;
            rotation = arrivalPoint.rotation;
            return;
        }

        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        fwd = fwd.sqrMagnitude < 0.001f ? Vector3.forward : fwd.normalized;

        position = transform.position + fwd * fallbackArrivalDistance + Vector3.up * 0.1f;
        rotation = Quaternion.LookRotation(fwd, Vector3.up);
    }

    private bool TryGetDestination(out Vector3 position, out Quaternion rotation, out LocationZone zone)
    {
        if (destinationOverride != null)
        {
            position = destinationOverride.position;
            rotation = destinationOverride.rotation;
            zone = destinationZoneOverride;
            return true;
        }

        if (linkedPortal != null)
        {
            linkedPortal.GetArrival(out position, out rotation);
            // Si el portal de destino está dentro de una cueva, se activa su zona.
            zone = linkedPortal.GetComponentInParent<LocationZone>(true);
            return true;
        }

        position = default;
        rotation = default;
        zone = null;
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null || !netObj.IsPlayerObject) return;

        var respawn = netObj.GetComponent<PlayerRespawnController>();
        if (respawn != null && respawn.IsDead.Value) return;

        ulong clientId = netObj.OwnerClientId;

        if (BlockedUntil.TryGetValue(clientId, out float until) && Time.time < until)
            return;

        if (!TryGetDestination(out Vector3 position, out Quaternion rotation, out LocationZone zone))
        {
            Debug.LogWarning($"[ScenePortal] '{name}' no está vinculado a ningún portal. " +
                             "Arrastrá otro portal al campo 'Linked Portal'.", this);
            return;
        }

        var teleport = netObj.GetComponent<PlayerTeleportController>();
        if (teleport == null)
        {
            Debug.LogError($"[ScenePortal] El jugador {netObj.name} no tiene PlayerTeleportController. " +
                           "Agregalo a los prefabs de jugador.", this);
            return;
        }

        BlockedUntil[clientId] = Time.time + cooldownSeconds;

        // Primero el destino (suelo/visuales), después el jugador.
        if (zone != null)
            zone.EnsureActive();

        teleport.TeleportServer(position, rotation);

        Debug.Log($"[ScenePortal] Cliente {clientId}: '{name}' -> {(linkedPortal != null ? linkedPortal.name : "destino manual")}");
    }

    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(0.8f, 0.3f, 1f, 0.5f);
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }

        // Dónde aparece quien llega a este portal.
        GetArrival(out Vector3 arrivalPos, out Quaternion arrivalRot);
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(arrivalPos, 0.35f);
        Gizmos.DrawRay(arrivalPos, arrivalRot * Vector3.forward * 1.2f);

        // Línea hacia el portal con el que está vinculado.
        Transform target = destinationOverride != null ? destinationOverride
                         : linkedPortal != null ? linkedPortal.transform
                         : null;

        if (target != null)
        {
            Gizmos.color = new Color(0.8f, 0.3f, 1f, 0.9f);
            Gizmos.DrawLine(transform.position + Vector3.up, target.position + Vector3.up);
        }
    }
}