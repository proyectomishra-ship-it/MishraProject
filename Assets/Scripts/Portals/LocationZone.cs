using UnityEngine;

/// <summary>
/// OPCIONAL. Enciende y apaga los objetos VISUALES de una ubicación (modelo de
/// Blender de una cueva, luces, partículas, decoración) según haya jugadores cerca.
/// Sirve igual para una cueva que para una parte del overworld.
///
/// NO controla enemigos: de eso se encarga únicamente EnemySleepManager. Así hay
/// un solo mecanismo para la IA y esta zona no puede contradecirlo.
///
/// Cada máquina (servidor y clientes) lo decide POR SU CUENTA mirando la posición
/// de los jugadores (PlayerTeleportController.Active, replicado por NetworkTransform).
/// Como todos ven las mismas posiciones, el resultado es el mismo sin RPCs ni
/// NetworkVariables, y los que se conectan tarde quedan bien solos.
///
/// QUÉ PONER EN "Toggled Objects":
///   - SÍ: modelos, luces, partículas, decoración.
///   - NO: NavMeshSurface, terreno/colliders que el servidor necesite al llegar
///         (si el collider del suelo está adentro, ScenePortal llama a
///         EnsureActive() antes de teletransportar), spawn points de enemigos,
///         ni ningún objeto con NetworkObject (apagarlo con SetActive no se replica).
/// </summary>
public class LocationZone : MonoBehaviour
{
    [Header("Zona")]
    [Tooltip("Centro de la ubicación. Si está vacío se usa este transform.")]
    [SerializeField] private Transform center;
    [Tooltip("Distancia a la que un jugador 'ocupa' la zona. Debe cubrir toda la ubicación.")]
    [SerializeField] private float activationRadius = 60f;
    [Tooltip("Segundos que la zona sigue activa después de quedar vacía.")]
    [SerializeField] private float deactivateDelay = 10f;
    [SerializeField] private float checkInterval = 0.25f;

    [Header("Estado inicial")]
    [Tooltip("Activar si los jugadores empiezan dentro de esta zona (ej. el overworld), para evitar un parpadeo al inicio.")]
    [SerializeField] private bool startActive;

    [Header("Objetos que se encienden/apagan")]
    [SerializeField] private GameObject[] toggledObjects;

    /// <summary>true mientras la zona está activa (hay jugadores o falta el delay de apagado).</summary>
    public bool IsOccupied { get; private set; }

    /// <summary>Objetos que esta zona enciende y apaga (lo usan las herramientas del editor).</summary>
    public GameObject[] ToggledObjects => toggledObjects;

    /// <summary>Centro de la zona en el mundo.</summary>
    public Vector3 CenterPosition => (center != null ? center : transform).position;

    private float timer;
    private float lastPresenceTime;

    private void Awake()
    {
        // Escalonar para que varias zonas no evalúen todas en el mismo frame.
        timer = Random.Range(0f, checkInterval);

        if (startActive)
        {
            IsOccupied = true;
            lastPresenceTime = Time.time; // período de gracia hasta que aparezcan los jugadores
            SetVisuals(true);
        }
        else
        {
            SetVisuals(false);
        }
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = checkInterval;

        if (AnyPlayerInside())
        {
            lastPresenceTime = Time.time;
            if (!IsOccupied) SetOccupied(true);
        }
        else if (IsOccupied && Time.time - lastPresenceTime >= deactivateDelay)
        {
            SetOccupied(false);
        }
    }

    /// <summary>
    /// Fuerza la activación inmediata (lo llama ScenePortal justo antes de
    /// teletransportar). Si finalmente nadie llega, se apaga sola tras el delay.
    /// </summary>
    public void EnsureActive()
    {
        lastPresenceTime = Time.time;
        if (!IsOccupied) SetOccupied(true);
    }

    private bool AnyPlayerInside()
    {
        Vector3 c = CenterPosition;
        float sqr = activationRadius * activationRadius;

        var players = PlayerTeleportController.Active;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;
            if ((p.transform.position - c).sqrMagnitude <= sqr) return true;
        }
        return false;
    }

    private void SetOccupied(bool value)
    {
        IsOccupied = value;
        SetVisuals(value);
    }

    private void SetVisuals(bool on)
    {
        if (toggledObjects == null) return;

        foreach (var go in toggledObjects)
            if (go != null && go.activeSelf != on)
                go.SetActive(on);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(CenterPosition, activationRadius);
    }
}