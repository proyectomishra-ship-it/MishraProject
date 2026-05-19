using UnityEngine;

public class EnemyStateIdle : EnemyState
{
    private float idleDuration;

    private float idleTimer;

    public EnemyStateIdle(
        Enemy enemy,
        EnemyAIController ai,
        float idleDuration)
        : base(enemy, ai)
    {
        this.idleDuration = idleDuration;
    }

    // =========================
    // ENTER
    // =========================

    public override void OnEnter()
    {
        if (!enemy.IsServer)
            return;

        idleTimer = 0f;

        if (ai.Agent != null &&
            ai.Agent.isOnNavMesh)
        {
            ai.Agent.ResetPath();
        }

        Debug.Log(
            $"[{enemy.name}] Idle");
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

        idleTimer += Time.deltaTime;

        if (idleTimer < idleDuration)
            return;

        if (ai.HasPatrolPoints)
        {
            ai.StateMachine.ChangeState(
                ai.PatrolState);
        }
        else
        {
            idleTimer = 0f;
        }
    }
}