using UnityEngine;
using UnityEngine.AI;

public class EnemyStateIdle : EnemyState
{
    private float idleDuration;
    private float idleTimer;

    public EnemyStateIdle(Enemy enemy, EnemyAIController ai, float idleDuration)
        : base(enemy, ai)
    {
        this.idleDuration = idleDuration;
    }

    public override void OnEnter()
    {
        idleTimer = 0f;
        ai.Agent.ResetPath();
        Debug.Log($"[{enemy.name}] Idle");
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

  
        idleTimer += Time.deltaTime;
        if (idleTimer >= idleDuration)
        {
            if (ai.HasPatrolPoints)
                ai.StateMachine.ChangeState(ai.PatrolState);
        }
    }
}