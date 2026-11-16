using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Mata a los jugadores que salen de los limites del mapa y los manda al
/// SafePoint mas cercano (a traves del flujo normal de muerte/respawn).
///
/// USO: colocar UNA vez en la escena (GameObject vacio + este componente) y
/// configurar los limites en el Inspector. La posicion del GameObject es solo
/// el centro del area que dibujan los gizmos; no afecta a los limites.
///
/// LIMITES PRINCIPALES: altura (eje Y), superior e inferior.
/// LIMITES EXTRA: horizontales (+X, -X, +Z, -Z). Todos apagados por defecto.
///
/// Todos los limites vienen DESACTIVADOS: hay que tildarlos a proposito para
/// que no maten a nadie por sorpresa si el mapa esta a otra altura.
///
/// En el Scene View, con este objeto seleccionado, cada limite activo tiene
/// un handle (cubo) que se arrastra a lo largo de su eje.
///
/// Toda la logica corre en el SERVIDOR. Los clientes no hacen nada.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("RPG/Map Bounds Killer")]
public class MapBoundsKiller : MonoBehaviour
{
    // =========================================================
    // DESCRIPCION DE UN LIMITE (la usan el gizmo y el editor)
    // =========================================================

    public struct Limit
    {
        public string Label;     // "Limite superior (eje Y)"
        public string Property;  // nombre del campo serializado (para el handle)
        public Vector3 Axis;     // eje positivo: up, right o forward
        public char AxisName;    // 'X', 'Y' o 'Z'
        public bool IsMax;       // true: muere por ENCIMA del valor
        public bool Enabled;
        public float Value;

        public bool IsExceeded(Vector3 position)
        {
            float coordinate = Vector3.Dot(position, Axis);
            return IsMax ? coordinate > Value : coordinate < Value;
        }

        public string Describe()
        {
            return $"muere si {AxisName} {(IsMax ? ">" : "<")} {Value:0.##}";
        }
    }

    // =========================================================
    // CONFIGURACION
    // =========================================================

    [Header("GENERAL")]
    [Tooltip("Cada cuantos segundos se revisa la posicion de los jugadores.")]
    [SerializeField, Min(0.05f)] private float checkInterval = 0.25f;

    [Header("PRINCIPAL · LIMITES DE ALTURA (eje Y)")]
    [Tooltip("Eje Y. El jugador muere si su altura (Y) supera este valor.")]
    [SerializeField] private bool enableUpperLimit = false;
    [SerializeField] private float upperLimitY = 200f;

    [Tooltip("Eje Y. El jugador muere si su altura (Y) queda por debajo de este valor.")]
    [SerializeField] private bool enableLowerLimit = false;
    [SerializeField] private float lowerLimitY = -50f;

    [Header("EXTRA · LIMITE EN EL EJE +X  (muere si X > valor)")]
    [SerializeField] private bool enableMaxX = false;
    [SerializeField] private float maxX = 500f;

    [Header("EXTRA · LIMITE EN EL EJE -X  (muere si X < valor)")]
    [SerializeField] private bool enableMinX = false;
    [SerializeField] private float minX = -500f;

    [Header("EXTRA · LIMITE EN EL EJE +Z  (muere si Z > valor)")]
    [SerializeField] private bool enableMaxZ = false;
    [SerializeField] private float maxZ = 500f;

    [Header("EXTRA · LIMITE EN EL EJE -Z  (muere si Z < valor)")]
    [SerializeField] private bool enableMinZ = false;
    [SerializeField] private float minZ = -500f;

    [Header("GIZMOS")]
    [Tooltip("Ancho del area dibujada en los ejes que no tienen limite " +
             "activo. Se centra en la posicion de este GameObject.")]
    [SerializeField, Min(1f)] private float gizmoSize = 300f;

    [Tooltip("Dibujar los planos solo cuando este objeto esta seleccionado.")]
    [SerializeField] private bool drawOnlyWhenSelected = false;

    // =========================================================
    // ESTADO
    // =========================================================

    private readonly Limit[] limitsCache = new Limit[6];
    private readonly List<string> errorBuffer = new List<string>();

    private float timer;
    private bool warnedNoSafePoints;
    private bool safePointsValidated;
    private bool warnedConfigErrors;

    // =========================================================
    // API (tambien la usa el editor)
    // =========================================================

    /// <summary>Los 6 limites posibles, con su estado actual.</summary>
    public Limit[] GetLimits()
    {
        limitsCache[0] = new Limit
        {
            Label = "Límite superior (eje Y)", Property = nameof(upperLimitY),
            Axis = Vector3.up, AxisName = 'Y', IsMax = true,
            Enabled = enableUpperLimit, Value = upperLimitY
        };
        limitsCache[1] = new Limit
        {
            Label = "Límite inferior (eje Y)", Property = nameof(lowerLimitY),
            Axis = Vector3.up, AxisName = 'Y', IsMax = false,
            Enabled = enableLowerLimit, Value = lowerLimitY
        };
        limitsCache[2] = new Limit
        {
            Label = "EXTRA · Límite +X", Property = nameof(maxX),
            Axis = Vector3.right, AxisName = 'X', IsMax = true,
            Enabled = enableMaxX, Value = maxX
        };
        limitsCache[3] = new Limit
        {
            Label = "EXTRA · Límite -X", Property = nameof(minX),
            Axis = Vector3.right, AxisName = 'X', IsMax = false,
            Enabled = enableMinX, Value = minX
        };
        limitsCache[4] = new Limit
        {
            Label = "EXTRA · Límite +Z", Property = nameof(maxZ),
            Axis = Vector3.forward, AxisName = 'Z', IsMax = true,
            Enabled = enableMaxZ, Value = maxZ
        };
        limitsCache[5] = new Limit
        {
            Label = "EXTRA · Límite -Z", Property = nameof(minZ),
            Axis = Vector3.forward, AxisName = 'Z', IsMax = false,
            Enabled = enableMinZ, Value = minZ
        };

        return limitsCache;
    }

    public bool AnyLimitEnabled
    {
        get
        {
            Limit[] limits = GetLimits();

            for (int i = 0; i < limits.Length; i++)
                if (limits[i].Enabled) return true;

            return false;
        }
    }

    /// <summary>True si la posicion esta fuera de algun limite activo.</summary>
    public bool IsOutside(Vector3 position, out Limit exceeded)
    {
        Limit[] limits = GetLimits();

        for (int i = 0; i < limits.Length; i++)
        {
            if (limits[i].Enabled && limits[i].IsExceeded(position))
            {
                exceeded = limits[i];
                return true;
            }
        }

        exceeded = default;
        return false;
    }

    /// <summary>
    /// Errores de configuracion (limites invertidos). Con errores, el script
    /// no mata a nadie: un limite invertido mataria a todos.
    /// </summary>
    public void GetConfigErrors(List<string> errors)
    {
        if (enableUpperLimit && enableLowerLimit && upperLimitY <= lowerLimitY)
            errors.Add("Eje Y: el límite superior debe ser MAYOR que el inferior.");

        if (enableMaxX && enableMinX && maxX <= minX)
            errors.Add("Eje X: el límite +X debe ser MAYOR que el límite -X.");

        if (enableMaxZ && enableMinZ && maxZ <= minZ)
            errors.Add("Eje Z: el límite +Z debe ser MAYOR que el límite -Z.");
    }

    private bool HasConfigErrors()
    {
        errorBuffer.Clear();
        GetConfigErrors(errorBuffer);
        return errorBuffer.Count > 0;
    }

    /// <summary>
    /// Caja donde se dibujan los planos. En cada eje usa los limites activos;
    /// donde no hay limite, usa el centro del objeto +/- gizmoSize/2.
    /// </summary>
    public Bounds GetDrawBounds()
    {
        Vector3 c = transform.position;
        float h = gizmoSize * 0.5f;

        float xA = enableMinX ? minX : c.x - h;
        float xB = enableMaxX ? maxX : c.x + h;
        float yA = enableLowerLimit ? lowerLimitY : c.y - h;
        float yB = enableUpperLimit ? upperLimitY : c.y + h;
        float zA = enableMinZ ? minZ : c.z - h;
        float zB = enableMaxZ ? maxZ : c.z + h;

        var bounds = new Bounds();
        bounds.SetMinMax(
            new Vector3(Mathf.Min(xA, xB), Mathf.Min(yA, yB), Mathf.Min(zA, zB)),
            new Vector3(Mathf.Max(xA, xB), Mathf.Max(yA, yB), Mathf.Max(zA, zB)));

        return bounds;
    }

    public static Color ColorFor(Vector3 axis)
    {
        if (axis == Vector3.up) return new Color(1f, 0.25f, 0.25f);   // altura: rojo
        if (axis == Vector3.right) return new Color(1f, 0.6f, 0.1f);  // X: naranja
        return new Color(0.8f, 0.3f, 1f);                             // Z: violeta
    }

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    private void Start()
    {
        errorBuffer.Clear();
        GetConfigErrors(errorBuffer);

        foreach (string error in errorBuffer)
            Debug.LogError($"[MapBoundsKiller] Configuración inválida: {error}", this);
    }

    private void Update()
    {
        NetworkManager nm = NetworkManager.Singleton;

        // Solo el servidor decide quien muere.
        if (nm == null || !nm.IsServer)
            return;

        timer += Time.deltaTime;

        if (timer < checkInterval)
            return;

        timer = 0f;

        CheckPlayers();
    }

    // =========================================================
    // CHEQUEO (SERVIDOR)
    // =========================================================

    private void CheckPlayers()
    {
        if (!AnyLimitEnabled)
            return;

        if (HasConfigErrors())
        {
            if (!warnedConfigErrors)
            {
                warnedConfigErrors = true;
                Debug.LogError(
                    "[MapBoundsKiller] Hay errores de configuración: no se mata a nadie " +
                    "hasta corregirlos (ver el Inspector).", this);
            }
            return;
        }

        warnedConfigErrors = false;

        // Sin SafePoints el jugador reviviria en el mismo lugar prohibido y
        // moriria en bucle. Mejor no matar a nadie.
        if (SafePoint.All.Count == 0)
        {
            if (!warnedNoSafePoints)
            {
                warnedNoSafePoints = true;
                Debug.LogError(
                    "[MapBoundsKiller] No hay SafePoints en la escena: no se mata a nadie " +
                    "fuera de los límites (reviviría en el mismo lugar y moriría en bucle). " +
                    "Agregá al menos un SafePoint dentro de los límites.", this);
            }
            return;
        }

        warnedNoSafePoints = false;
        ValidateSafePointsOnce();

        List<PlayerTeleportController> players = PlayerTeleportController.Active;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerTeleportController entry = players[i];

            if (entry == null)
                continue;

            Player player = entry.GetComponent<Player>();

            if (player == null || player.IsDead())
                continue;

            Vector3 position = player.transform.position;

            if (!IsOutside(position, out Limit exceeded))
                continue;

            Debug.Log(
                $"[MapBoundsKiller] {player.name} fuera de los límites " +
                $"({exceeded.Label}: {exceeded.Describe()}). Posición: {position}.", this);

            // Mismo flujo que la muerte por daño: respawn en el SafePoint
            // mas cercano. Ignora la invulnerabilidad post-respawn a proposito.
            player.StartRespawn();
        }
    }

    /// <summary>
    /// Avisa (una vez) de los SafePoints que quedan fuera de los limites:
    /// el jugador moriria de nuevo apenas reviva.
    /// </summary>
    private void ValidateSafePointsOnce()
    {
        if (safePointsValidated)
            return;

        safePointsValidated = true;

        foreach (SafePoint point in SafePoint.All)
        {
            if (point != null && IsOutside(point.Position, out Limit exceeded))
            {
                Debug.LogError(
                    $"[MapBoundsKiller] El SafePoint '{point.name}' está FUERA de los límites " +
                    $"({exceeded.Label}). El jugador moriría de nuevo al revivir ahí.", point);
            }
        }
    }

    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmos()
    {
        bool selected = false;

#if UNITY_EDITOR
        selected = UnityEditor.Selection.Contains(gameObject);
#endif

        if (drawOnlyWhenSelected && !selected)
            return;

        Bounds area = GetDrawBounds();
        float arrowLength = Mathf.Max(2f, gizmoSize * 0.04f);

        Limit[] limits = GetLimits();

        for (int i = 0; i < limits.Length; i++)
        {
            Limit limit = limits[i];

            if (!limit.Enabled)
                continue;

            Color color = ColorFor(limit.Axis);

            // Plano: la caja achatada sobre el eje del limite, en su valor.
            Vector3 center = area.center;
            center = center - Vector3.Scale(center, limit.Axis) + limit.Axis * limit.Value;

            Vector3 size = area.size;
            size = size - Vector3.Scale(size, limit.Axis) + limit.Axis * 0.05f;

            Gizmos.color = new Color(color.r, color.g, color.b, selected ? 0.22f : 0.12f);
            Gizmos.DrawCube(center, size);

            Gizmos.color = color;
            Gizmos.DrawWireCube(center, size);

            // Flecha hacia el lado donde se muere.
            Vector3 killDirection = limit.IsMax ? limit.Axis : -limit.Axis;
            Vector3 tip = center + killDirection * arrowLength;
            Vector3 sideways = Mathf.Abs(Vector3.Dot(killDirection, Vector3.up)) > 0.9f
                ? Vector3.right
                : Vector3.up;

            Gizmos.DrawLine(center, tip);
            Gizmos.DrawLine(tip, tip - killDirection * arrowLength * 0.3f + sideways * arrowLength * 0.15f);
            Gizmos.DrawLine(tip, tip - killDirection * arrowLength * 0.3f - sideways * arrowLength * 0.15f);

#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(
                center + Vector3.up * arrowLength * 0.3f,
                $"{limit.Label}\n{limit.Describe()}");
#endif
        }
    }
}
