using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lógica de gameplay (solo servidor) de las habilidades especiales:
/// buscar enemigos en un área y aplicarles daño.
/// No toca recursos, cooldown ni animaciones: eso lo orquesta PlayerCombatController.
/// </summary>
public static class SpecialAbilityEffects
{
    // =========================================================
    // BÚSQUEDA DE OBJETIVOS
    // =========================================================

    /// <summary>Todos los enemigos vivos dentro de una esfera.</summary>
    public static List<Character> FindEnemiesInRadius(
        Character caster,
        Vector3 center,
        float radius,
        LayerMask enemyLayer)
    {
        var result = new List<Character>();
        var seen = new HashSet<Character>();

        Collider[] hits = Physics.OverlapSphere(
            center,
            radius,
            enemyLayer,
            QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            // Un enemigo puede tener varios colliders: se cuenta una sola vez.
            Character candidate = hit.GetComponentInParent<Character>();

            if (!IsValidTarget(caster, candidate) || !seen.Add(candidate))
                continue;

            result.Add(candidate);
        }

        return result;
    }

    /// <summary>
    /// Todos los enemigos vivos dentro de un arco frontal.
    /// El ángulo se mide en el plano horizontal, así mirar un poco hacia arriba
    /// o abajo con la cámara no deja enemigos fuera del golpe.
    /// </summary>
    public static List<Character> FindEnemiesInCone(
        Character caster,
        Vector3 aimDirection,
        float range,
        float arcAngle,
        LayerMask enemyLayer)
    {
        var result = new List<Character>();
        var seen = new HashSet<Character>();

        Vector3 origin = caster.transform.position;
        Vector3 forward = Flatten(aimDirection, caster.transform.forward);
        float halfArc = arcAngle * 0.5f;

        Collider[] hits = Physics.OverlapSphere(
            origin,
            range,
            enemyLayer,
            QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            Character candidate = hit.GetComponentInParent<Character>();

            if (!IsValidTarget(caster, candidate) || seen.Contains(candidate))
                continue;

            // Punto del collider más cercano al jugador: enemigos grandes
            // (ej. el DemiGod) cuentan aunque su centro quede fuera del arco.
            Vector3 toTarget = hit.bounds.ClosestPoint(origin) - origin;
            toTarget.y = 0f;

            // Enemigo pegado al jugador: siempre dentro del arco.
            if (toTarget.sqrMagnitude > 0.0001f &&
                Vector3.Angle(forward, toTarget) > halfArc)
            {
                continue;
            }

            seen.Add(candidate);
            result.Add(candidate);
        }

        return result;
    }

    // =========================================================
    // DAÑO
    // =========================================================

    /// <summary>Aplica el daño a cada objetivo. Devuelve cuántos lo recibieron.</summary>
    public static int ApplyDamage(
        List<Character> targets,
        Character caster,
        float damage)
    {
        int hits = 0;

        foreach (Character target in targets)
        {
            // Un objetivo pudo morir o despawnearse entre la búsqueda y el daño.
            if (target == null)
                continue;

            DamageReceiver receiver = target.GetComponent<DamageReceiver>();

            if (receiver == null)
                continue;

            receiver.TakeDamage(damage, caster);
            hits++;
        }

        return hits;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static bool IsValidTarget(Character caster, Character candidate)
    {
        return candidate != null
            && candidate != caster
            && candidate is not Player          // el daño de habilidades no alcanza a aliados
            && candidate.GetStats() != null
            && candidate.GetStats().CurrentHealth > 0f;
    }

    /// <summary>Proyecta una dirección al plano horizontal. Si queda vacía usa el fallback.</summary>
    public static Vector3 Flatten(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            direction = fallback;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.001f)
            direction = Vector3.forward;

        return direction.normalized;
    }

    /// <summary>Desvía una dirección un ángulo aleatorio de hasta maxAngle grados.</summary>
    public static Vector3 ApplySpread(Vector3 direction, float maxAngle)
    {
        if (maxAngle <= 0f)
            return direction;

        Vector3 right = Vector3.Cross(Vector3.up, direction);

        // Apuntando casi vertical el producto cruzado se anula.
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.right;

        right.Normalize();

        float yaw = Random.Range(-maxAngle, maxAngle);
        float pitch = Random.Range(-maxAngle * 0.5f, maxAngle * 0.5f);

        return Quaternion.AngleAxis(yaw, Vector3.up)
             * Quaternion.AngleAxis(pitch, right)
             * direction;
    }
}
