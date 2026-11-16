using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Punto seguro de respawn. Al morir, el jugador revive en el SafePoint
/// activo más cercano (ver PlayerRespawnController).
///
/// USO: crear un GameObject vacío en el mapa, agregarle este componente y
/// moverlo/rotarlo. La flecha del gizmo indica hacia dónde mira el jugador
/// al revivir (eje Z azul del transform).
///
/// Se registra solo en una lista estática, así que no hace falta referenciarlo
/// desde ningún lado. Para desactivar un punto, desactivar el GameObject.
///
/// No es un objeto de red: existe en la escena de todas las máquinas y solo
/// el servidor consulta su posición.
/// </summary>
[AddComponentMenu("RPG/Safe Point")]
public class SafePoint : MonoBehaviour
{
    // =========================================================
    // REGISTRO ESTÁTICO
    // =========================================================

    private static readonly List<SafePoint> all = new List<SafePoint>();

    public static IReadOnlyList<SafePoint> All => all;

    // Con "Enter Play Mode Options" sin domain reload, las estáticas
    // sobreviven entre sesiones de Play. Esto las limpia.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => all.Clear();

    /// <summary>
    /// Devuelve el SafePoint activo más cercano (distancia 3D) a una posición.
    /// False si no hay ninguno en la escena.
    /// </summary>
    public static bool TryGetNearest(Vector3 from, out SafePoint nearest)
    {
        nearest = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < all.Count; i++)
        {
            SafePoint point = all[i];

            if (point == null)
                continue;

            float sqr = (point.Position - from).sqrMagnitude;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = point;
            }
        }

        return nearest != null;
    }

    // =========================================================
    // CONFIGURACIÓN
    // =========================================================

    [Header("Respawn")]
    [Tooltip("Altura extra sobre el transform donde aparece el jugador. " +
             "Evita que quede incrustado en el piso si el punto está justo " +
             "sobre el terreno.")]
    [SerializeField, Min(0f)] private float spawnHeightOffset = 0.1f;

    [Header("Gizmo")]
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.9f, 0.3f, 1f);
    [SerializeField] private bool showLabel = true;

    /// <summary>Posición donde aparece el jugador.</summary>
    public Vector3 Position => transform.position + Vector3.up * spawnHeightOffset;

    /// <summary>Hacia dónde mira el jugador al revivir (solo el giro en Y).</summary>
    public Quaternion Rotation => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    private void OnEnable()
    {
        if (!all.Contains(this))
            all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmos() => DrawGizmo(selected: false);

    private void OnDrawGizmosSelected() => DrawGizmo(selected: true);

    private void DrawGizmo(bool selected)
    {
        Vector3 pos = Position;

        Color color = gizmoColor;
        color.a = selected ? 1f : 0.7f;
        Gizmos.color = color;

        // Base: círculo en el piso del punto.
        DrawCircle(transform.position, 0.5f);

        // Columna con la altura aproximada de un jugador.
        Gizmos.DrawLine(pos, pos + Vector3.up * 1.8f);
        Gizmos.DrawWireSphere(pos + Vector3.up * 1.8f, 0.15f);

        // Flecha: hacia dónde mira el jugador al revivir.
        Vector3 forward = Rotation * Vector3.forward;
        Vector3 tip = pos + forward * 1f;
        Gizmos.DrawLine(pos, tip);
        Gizmos.DrawLine(tip, tip - (forward + Rotation * Vector3.right * 0.5f) * 0.3f);
        Gizmos.DrawLine(tip, tip - (forward - Rotation * Vector3.right * 0.5f) * 0.3f);

        if (selected)
            Gizmos.DrawWireSphere(pos, 0.25f);

#if UNITY_EDITOR
        if (showLabel)
        {
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(pos + Vector3.up * 2.1f, $"Safe Point\n{name}");
        }
#endif
    }

    private static void DrawCircle(Vector3 center, float radius)
    {
        const int segments = 24;
        Vector3 previous = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 next = center + new Vector3(
                Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
