using UnityEngine;

public class EnemyPerceptionSystem
{
    private Enemy enemy;

   
    private float detectionRadius;

    private float fieldOfViewAngle;

 
    private float alertRadius;

    public EnemyPerceptionSystem(Enemy enemy, float detectionRadius,
        float fieldOfViewAngle, float alertRadius)
    {
        this.enemy = enemy;
        this.detectionRadius = detectionRadius;
        this.fieldOfViewAngle = fieldOfViewAngle;
        this.alertRadius = alertRadius;
    }

 
    public Character DetectPlayer(bool isAlerted)
    {
        
        float radius = isAlerted ? alertRadius : detectionRadius;
        Collider[] hits = Physics.OverlapSphere(
            enemy.transform.position, radius);

        Character closest = null;
        float closestDistance = float.MaxValue;

        foreach (var hit in hits)
        {
            Character target = hit.GetComponent<Character>();
            if (target == null || target == enemy) continue;
            if (target is not Player) continue;

            float distance = Vector3.Distance(
                enemy.transform.position, target.transform.position);

            
            if (isAlerted && distance < closestDistance)
            {
                closest = target;
                closestDistance = distance;
                continue;
            }

            if (IsInFieldOfView(target.transform.position) &&
                distance < closestDistance)
            {
                closest = target;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private bool IsInFieldOfView(Vector3 targetPosition)
    {
        Vector3 directionToTarget =
            (targetPosition - enemy.transform.position).normalized;

        float angle = Vector3.Angle(
            enemy.transform.forward, directionToTarget);

        return angle <= fieldOfViewAngle / 2f;
    }

   
    public float DetectionRadius => detectionRadius;
    public float FieldOfViewAngle => fieldOfViewAngle;
    public float AlertRadius => alertRadius;
}