using UnityEngine;
using UnityEngine.AI;

public class EnemyStatePatrol : EnemyState
{
    private float waypointReachedThreshold = 0.5f;

    public EnemyStatePatrol(Enemy enemy, EnemyAIController ai)
        : base(enemy, ai) { }

    public override void OnEnter()
    {
        Debug.Log($"[{enemy.name}] Patrol");
        MoveToNextWaypoint();
    }

    public override void OnUpdate()
    {
  
        Character target = ai.Perception.DetectPlayer(ai.IsAlerted);
        if (target != null)
        {
            ai.SetTarget(target);
            ai.StateMachine.ChangeState(ai.ChaseState);
            return;
        }


        if (!ai.Agent.pathPending &&
            ai.Agent.remainingDistance <= waypointReachedThreshold)
        {
            ai.AdvanceWaypoint();
            MoveToNextWaypoint();
        }
    }

    private void MoveToNextWaypoint()
    {
        Vector3 destination = ai.GetCurrentWaypoint();
        ai.Agent.SetDestination(destination);
    }
}