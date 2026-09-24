using UnityEngine;

public class EnemyPerceptionSystem
{
    private Enemy enemy;

    private float detectionRadius;
    private float fieldOfViewAngle;
    private float alertRadius;

    public EnemyPerceptionSystem(
        Enemy enemy,
        float detectionRadius,
        float fieldOfViewAngle,
        float alertRadius)
    {
        this.enemy = enemy;
        this.detectionRadius = detectionRadius;
        this.fieldOfViewAngle = fieldOfViewAngle;
        this.alertRadius = alertRadius;
    }

    // =========================================================
    // DETECT PLAYER
    // =========================================================

    public Character DetectPlayer(bool isAlerted)
    {
        float radius =
            isAlerted
                ? alertRadius
                : detectionRadius;

        Collider[] hits =
            Physics.OverlapSphere(
                enemy.transform.position,
                radius
            );

        Character closest = null;
        float closestDistance = float.MaxValue;

        foreach (var hit in hits)
        {
            // =================================================
            // IMPORTANTE
            //
            // El collider puede estar en un hijo del Player.
            // Por eso usamos GetComponentInParent.
            // =================================================

            Character target =
                hit.GetComponentInParent<Character>();

            if (target == null)
                continue;

            if (target == enemy)
                continue;

            if (target is not Player)
                continue;

            float distance =
                Vector3.Distance(
                    enemy.transform.position,
                    target.transform.position
                );

            // =================================================
            // ALERTED
            //
            // Si está alertado no necesitamos FOV.
            // =================================================

            if (isAlerted && distance < closestDistance)
            {
                closest = target;
                closestDistance = distance;
                continue;
            }

            // =================================================
            // FOV NORMAL
            // =================================================

            if (IsInFieldOfView(target.transform.position) &&
                distance < closestDistance)
            {
                closest = target;
                closestDistance = distance;
            }
        }

     

        return closest;
    }

    // =========================================================
    // FIELD OF VIEW
    // =========================================================

    private bool IsInFieldOfView(Vector3 targetPosition)
    {
        Vector3 directionToTarget =
            targetPosition - enemy.transform.position;

        directionToTarget.y = 0f;

        if (directionToTarget.sqrMagnitude <= 0.001f)
            return true;

        directionToTarget.Normalize();

        Vector3 forward =
            enemy.transform.forward;

        forward.y = 0f;

        forward.Normalize();

        float angle =
            Vector3.Angle(
                forward,
                directionToTarget
            );

        return angle <= fieldOfViewAngle / 2f;
    }

    // =========================================================
    // PROPERTIES
    // =========================================================

    public float DetectionRadius =>
        detectionRadius;

    public float FieldOfViewAngle =>
        fieldOfViewAngle;

    public float AlertRadius =>
        alertRadius;
}