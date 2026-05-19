using UnityEngine;

public class EnemyStatePatrol : EnemyState
{
    private float waypointReachedThreshold = 0.5f;

    // =========================
    // CICLO DE PATROL
    // =========================

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

        MoveToNextWaypoint();
    }

    public override void OnUpdate()
    {
        // =========================
        // DETECCIÓN
        // =========================

        Character target =
            ai.Perception.DetectPlayer(ai.IsAlerted);

        if (target != null)
        {
            ai.SetTarget(target);

            ai.StateMachine.ChangeState(ai.ChaseState);

            return;
        }

        // =========================
        // MOVIMIENTO
        // =========================

        if (ai.Agent.pathPending)
            return;

        if (ai.Agent.remainingDistance >
            waypointReachedThreshold)
            return;

        patrolCount++;

        Debug.Log(
            $"[{enemy.name}] Patrol point reached ({patrolCount}/{maxPatrolMoves})");

        // =========================
        // FIN DE CICLO
        // =========================

        if (patrolCount >= maxPatrolMoves)
        {
            Debug.Log($"[{enemy.name}] Patrol -> Idle");

            ai.StateMachine.ChangeState(ai.IdleState);

            return;
        }

        MoveToNextWaypoint();
    }

    private void MoveToNextWaypoint()
    {
        Vector3 destination = ai.GetCurrentWaypoint();

        ai.Agent.SetDestination(destination);

        Debug.Log(
            $"[{enemy.name}] Moving to patrol point: {destination}");
    }
}