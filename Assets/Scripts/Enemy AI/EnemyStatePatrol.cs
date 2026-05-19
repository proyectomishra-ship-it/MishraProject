using UnityEngine;

public class EnemyStatePatrol : EnemyState
{
    private float waypointReachedThreshold =
        0.5f;

    private int patrolCount;

    private int maxPatrolMoves;

    public EnemyStatePatrol(
        Enemy enemy,
        EnemyAIController ai,
        int maxPatrolMoves)
        : base(enemy, ai)
    {
        this.maxPatrolMoves =
            maxPatrolMoves;
    }

    // =========================
    // ENTER
    // =========================

    public override void OnEnter()
    {
        if (!enemy.IsServer)
            return;

        patrolCount = 0;

        Debug.Log(
            $"[{enemy.name}] Patrol");

        MoveToNextWaypoint();
    }

    // =========================
    // UPDATE
    // =========================

    public override void OnUpdate()
    {
        if (!enemy.IsServer)
            return;

        Character target =
            ai.Perception.DetectPlayer(
                ai.IsAlerted);

        if (target != null)
        {
            ai.SetTarget(target);

            ai.StateMachine.ChangeState(
                ai.ChaseState);

            return;
        }

        if (ai.Agent == null)
            return;

        if (!ai.Agent.isOnNavMesh)
            return;

        if (ai.Agent.pathPending)
            return;

        if (ai.Agent.remainingDistance >
            waypointReachedThreshold)
            return;

        patrolCount++;

        if (patrolCount >= maxPatrolMoves)
        {
            ai.StateMachine.ChangeState(
                ai.IdleState);

            return;
        }

        MoveToNextWaypoint();
    }

    // =========================
    // WAYPOINT
    // =========================

    private void MoveToNextWaypoint()
    {
        Vector3 destination =
            ai.GetCurrentWaypoint();

        ai.Agent.SetDestination(
            destination);

        Debug.Log(
            $"[{enemy.name}] Patrol -> {destination}");
    }
}