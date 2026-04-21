using UnityEngine;
using UnityEngine.AI;

public class EnemyStateChase : EnemyState
{
 
    private float lostTargetTime;
    private float lostTargetTimer;
    private bool targetLost;

    public EnemyStateChase(Enemy enemy, EnemyAIController ai, float lostTargetTime)
        : base(enemy, ai)
    {
        this.lostTargetTime = lostTargetTime;
    }

    public override void OnEnter()
    {
        lostTargetTimer = 0f;
        targetLost = false;
        Debug.Log($"[{enemy.name}] Chase → {ai.CurrentTarget?.name}");
    }

    public override void OnUpdate()
    {
        if (ai.ShouldFlee)
        {
            ai.StateMachine.ChangeState(ai.FleeState);
            return;
        }

        if (ai.CurrentTarget == null)
        {
            HandleLostTarget();
            return;
        }

        Character detected = ai.Perception.DetectPlayer(ai.IsAlerted);

        if (detected != null)
        {
            
            targetLost = false;
            lostTargetTimer = 0f;
            ai.Agent.SetDestination(ai.CurrentTarget.transform.position);

       
            float distanceToTarget = Vector3.Distance(
                enemy.transform.position,
                ai.CurrentTarget.transform.position);

            if (distanceToTarget <= enemy.GetStats().AttackRange.Value)
                ai.StateMachine.ChangeState(ai.AttackState);
        }
        else
        {
            HandleLostTarget();
        }
    }

    private void HandleLostTarget()
    {
        if (!targetLost)
        {
            targetLost = true;
            lostTargetTimer = 0f;
            ai.Agent.ResetPath();
            Debug.Log($"[{enemy.name}] Perdió al jugador, esperando...");
        }

        lostTargetTimer += Time.deltaTime;

        if (lostTargetTimer >= lostTargetTime)
        {
            ai.SetTarget(null);
            ai.SetAlerted(false);
            Debug.Log($"[{enemy.name}] Volviendo a patrulla");

            if (ai.HasPatrolPoints)
                ai.StateMachine.ChangeState(ai.PatrolState);
            else
                ai.StateMachine.ChangeState(ai.IdleState);
        }
    }

    public override void OnExit()
    {
        ai.Agent.ResetPath();
    }
}