using UnityEngine;
using UnityEngine.AI;

public class EnemyStatePatrol : EnemyState
{
    private float waypointReachedThreshold = 0.5f;

    private int patrolCount;

    [SerializeField]
    private int maxPatrolMoves = 6;

    public EnemyStatePatrol(
        Enemy enemy,
        EnemyAIController ai)
        : base(enemy, ai)
    {
    }

    public override void OnEnter()
    {
        patrolCount = 0;

        Debug.Log($"[{enemy.name}] Patrol");

    
        EnsureNavMeshReady();

        MoveToNextWaypoint();
    }

    private void EnsureNavMeshReady()
    {
        var agent = ai.Agent;

        if (agent == null)
            return;


        agent.ResetPath();
        agent.isStopped = false;


        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(ai.transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

 
        agent.ResetPath();
    }

    public override void OnUpdate()
    {
        Character target =
            ai.Perception.DetectPlayer(ai.IsAlerted);

        if (target != null)
        {
            ai.SetTarget(target);
            ai.StateMachine.ChangeState(ai.ChaseState);
            return;
        }

        if (ai.Agent.pathPending)
            return;

       
        if (!ai.Agent.hasPath)
            return;

        if (ai.Agent.remainingDistance > waypointReachedThreshold)
            return;

        patrolCount++;

        if (patrolCount >= maxPatrolMoves)
        {
            ai.StateMachine.ChangeState(ai.IdleState);
            return;
        }

        MoveToNextWaypoint();
    }

    private void MoveToNextWaypoint()
    {
        Vector3 destination = ai.GetCurrentWaypoint();

        ai.Agent.ResetPath();
        ai.Agent.SetDestination(destination);
    }
}