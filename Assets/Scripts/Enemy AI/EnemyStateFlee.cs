using UnityEngine;
using UnityEngine.AI;

public class EnemyStateFlee : EnemyState
{
    private float fleeDistance;
    private float updateDestinationInterval = 0.5f;
    private float updateTimer;

    public EnemyStateFlee(Enemy enemy, EnemyAIController ai, float fleeDistance)
        : base(enemy, ai)
    {
        this.fleeDistance = fleeDistance;
    }

    public override void OnEnter()
    {
        updateTimer = updateDestinationInterval;
        ai.Agent.speed *= 1.3f; 
        Debug.Log($"[{enemy.name}] Huyendo!");
        UpdateFleeDestination();
    }

    public override void OnUpdate()
    {
        
        if (!ai.ShouldFlee)
        {
            ai.Agent.speed /= 1.3f;
            ai.StateMachine.ChangeState(ai.IdleState);
            return;
        }

        
        updateTimer += Time.deltaTime;
        if (updateTimer >= updateDestinationInterval)
        {
            updateTimer = 0f;
            UpdateFleeDestination();
        }
    }

    public override void OnExit()
    {
        ai.Agent.speed /= 1.3f;
    }

    private void UpdateFleeDestination()
    {
      
        Character nearestPlayer = FindNearestPlayer();
        if (nearestPlayer == null) return;

     
        Vector3 directionAwayFromPlayer =
            (enemy.transform.position - nearestPlayer.transform.position).normalized;

        Vector3 fleeTarget = enemy.transform.position + directionAwayFromPlayer * fleeDistance;


        if (NavMesh.SamplePosition(fleeTarget, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
            ai.Agent.SetDestination(hit.position);
        else
            
            if (NavMesh.SamplePosition(fleeTarget, out NavMeshHit fallback,
                fleeDistance * 0.5f, NavMesh.AllAreas))
            ai.Agent.SetDestination(fallback.position);
    }

    private Character FindNearestPlayer()
    {
       
        if (ai.CurrentTarget != null)
            return ai.CurrentTarget;

        Collider[] hits = Physics.OverlapSphere(enemy.transform.position, 30f);
        Character nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.GetComponent<Character>() is Player player)
            {
                float dist = Vector3.Distance(enemy.transform.position,
                    player.transform.position);
                if (dist < nearestDist)
                {
                    nearest = player;
                    nearestDist = dist;
                }
            }
        }

        return nearest;
    }
}