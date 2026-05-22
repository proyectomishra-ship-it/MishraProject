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

    public override void OnEnter()
    {
        idleTimer = 0f;

        ai.Agent.ResetPath();

        //Debug.Log($"[{enemy.name}] Idle");
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
        // TIMER
        // =========================

        idleTimer += Time.deltaTime;

        if (idleTimer < idleDuration)
            return;

        // =========================
        // RETORNO A PATROL
        // =========================

        if (ai.HasPatrolPoints)
        {
            //Debug.Log($"[{enemy.name}] Idle -> Patrol");

            ai.StateMachine.ChangeState(ai.PatrolState);
        }
        else
        {
            
            idleTimer = 0f;
        }
    }
}