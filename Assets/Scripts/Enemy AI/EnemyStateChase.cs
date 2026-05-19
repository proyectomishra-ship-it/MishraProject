using UnityEngine;
using UnityEngine.AI;

public class EnemyStateChase : EnemyState
{
    private float lostTargetTime;

    private float lostTargetTimer;

    private bool targetLost;

    public EnemyStateChase(
        Enemy enemy,
        EnemyAIController ai,
        float lostTargetTime)
        : base(enemy, ai)
    {
        this.lostTargetTime = lostTargetTime;
    }

    // =========================
    // ENTER
    // =========================

    public override void OnEnter()
    {
        if (!enemy.IsServer)
            return;

        lostTargetTimer = 0f;

        targetLost = false;

        if (ai.Agent != null &&
            ai.Agent.isOnNavMesh)
        {
            ai.Agent.isStopped = false;
        }

        Debug.Log(
            $"[{enemy.name}] Chase -> {ai.CurrentTarget?.name}");
    }

    // =========================
    // UPDATE
    // =========================

    public override void OnUpdate()
    {
        if (!enemy.IsServer)
            return;

        if (ai.ShouldFlee)
        {
            ai.StateMachine.ChangeState(
                ai.FleeState);

            return;
        }

        if (ai.CurrentTarget == null)
        {
            HandleLostTarget();

            return;
        }

        Character detected =
            ai.Perception.DetectPlayer(
                ai.IsAlerted);

        if (detected != null)
        {
            targetLost = false;

            lostTargetTimer = 0f;

            UpdateDestination();

            float distance =
                Vector3.Distance(
                    enemy.transform.position,
                    ai.CurrentTarget.transform.position);

            if (distance <=
                enemy.GetStats().AttackRange.Value)
            {
                ai.StateMachine.ChangeState(
                    ai.AttackState);
            }
        }
        else
        {
            HandleLostTarget();
        }
    }

    // =========================
    // DESTINATION
    // =========================

    private void UpdateDestination()
    {
        if (ai.Agent == null)
            return;

        if (!ai.Agent.isOnNavMesh)
            return;

        ai.Agent.SetDestination(
            ai.CurrentTarget.transform.position);
    }

    // =========================
    // LOST TARGET
    // =========================

    private void HandleLostTarget()
    {
        if (!targetLost)
        {
            targetLost = true;

            lostTargetTimer = 0f;

            if (ai.Agent != null &&
                ai.Agent.isOnNavMesh)
            {
                ai.Agent.ResetPath();
            }

            Debug.Log(
                $"[{enemy.name}] Lost target");
        }

        lostTargetTimer += Time.deltaTime;

        if (lostTargetTimer < lostTargetTime)
            return;

        ai.SetTarget(null);

        ai.SetAlerted(false);

        if (ai.HasPatrolPoints)
        {
            ai.StateMachine.ChangeState(
                ai.PatrolState);
        }
        else
        {
            ai.StateMachine.ChangeState(
                ai.IdleState);
        }
    }

    // =========================
    // EXIT
    // =========================

    public override void OnExit()
    {
        if (ai.Agent != null &&
            ai.Agent.isOnNavMesh)
        {
            ai.Agent.ResetPath();
        }
    }
}