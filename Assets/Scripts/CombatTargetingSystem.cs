using UnityEngine;

public static class CombatTargetingSystem
{

    // =========================
    // TARGETING SERVER SIDE
    // =========================
    public static Character FindBestTarget(
        Character attacker,
        float range,
        float coneAngle,
        LayerMask enemyLayer)
    {
        Collider[] hits = Physics.OverlapSphere(
            attacker.transform.position,
            range,
            enemyLayer
        );

        if (hits.Length == 0)
            return null;

        Character bestTarget = null;
        float bestScore = float.MaxValue;

        foreach (var hit in hits)
        {
            Character candidate = hit.GetComponent<Character>();
            if (candidate == null || candidate == attacker)
                continue;

            Vector3 dirToTarget = (candidate.transform.position - attacker.transform.position).normalized;

            float angle = Vector3.Angle(attacker.transform.forward, dirToTarget);
            if (angle > coneAngle)
                continue;

            float distance = Vector3.Distance(
                attacker.transform.position,
                candidate.transform.position
            );

            float score = (angle * 2f) + distance;

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }
}