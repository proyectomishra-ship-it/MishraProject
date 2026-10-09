using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Revisa la escena abierta y avisa de errores típicos al armar portales y cuevas.
/// Menú: Tools > Portals > Validar vínculos de la escena.
///
/// Detecta:
///   - Portales sin vincular a nada.
///   - Vínculos cruzados: A apunta a B pero B apunta a C.
///   - Dos portales que apuntan al mismo destino.
///   - Portales dentro de objetos que una LocationZone apaga (el trigger dejaría de funcionar).
/// </summary>
public static class PortalValidator
{
    [MenuItem("Tools/Portals/Validar vínculos de la escena")]
    public static void Validate()
    {
        var portals = Object.FindObjectsByType<ScenePortal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var zones = Object.FindObjectsByType<LocationZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int problems = 0;
        var targetedBy = new Dictionary<ScenePortal, ScenePortal>();

        foreach (var p in portals)
        {
            if (p.HasManualDestination) continue; // destino manual: se asume correcto

            var linked = p.LinkedPortal;

            if (linked == null)
            {
                // Puede estar vinculado desde el otro lado (el vínculo es bidireccional).
                bool linkedFromOther = false;
                foreach (var q in portals)
                {
                    if (q != p && q.LinkedPortal == p) { linkedFromOther = true; break; }
                }

                if (!linkedFromOther)
                {
                    Warn(p, "no está vinculado a ningún portal. Arrastrá otro portal a 'Linked Portal'.");
                    problems++;
                }
                continue;
            }

            if (linked == p)
            {
                Warn(p, "está vinculado consigo mismo.");
                problems++;
                continue;
            }

            if (linked.LinkedPortal != null && linked.LinkedPortal != p)
            {
                Warn(p, $"apunta a '{linked.name}', pero ese portal apunta a '{linked.LinkedPortal.name}'. " +
                        "El vínculo no es recíproco.");
                problems++;
            }

            if (targetedBy.TryGetValue(linked, out var other) && other != p)
            {
                Warn(p, $"apunta a '{linked.name}', igual que '{other.name}'. Dos portales no pueden compartir destino.");
                problems++;
            }
            else
            {
                targetedBy[linked] = p;
            }
        }

        foreach (var zone in zones)
        {
            foreach (var go in zone.ToggledObjects)
            {
                if (go == null) continue;

                foreach (var p in go.GetComponentsInChildren<ScenePortal>(true))
                {
                    Warn(p, $"está dentro de '{go.name}', que la zona '{zone.name}' apaga. " +
                            "Sacalo de ahí: el trigger tiene que estar siempre activo.");
                    problems++;
                }
            }
        }

        if (problems == 0)
            Debug.Log($"[Portales] OK: {portals.Length} portal(es) y {zones.Length} zona(s) sin problemas.");
        else
            Debug.LogWarning($"[Portales] {problems} problema(s) encontrado(s). Hacé click en cada aviso para ir al objeto.");
    }

    private static void Warn(Object context, string message)
    {
        Debug.LogWarning($"[Portales] '{context.name}' {message}", context);
    }
}
