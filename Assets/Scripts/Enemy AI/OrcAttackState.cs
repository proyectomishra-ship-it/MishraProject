using UnityEngine;
using UnityEngine.AI;

public class OrcAttackState : EnemyStateAttack
{
    private float heavyAttackCooldown = 4f;
    private float heavyAttackTimer;

 

    public OrcAttackState(Enemy enemy, EnemyAIController ai)
        : base(enemy, ai, attackCooldown: 1.8f)
    {
        heavyAttackTimer = heavyAttackCooldown;
    }

    public override void OnEnter()
    {
        base.OnEnter();
       
        ai.Agent.angularSpeed = 80f;
        ai.Agent.speed = enemy.GetStats().Speed.Value;
    }

    public override void OnUpdate()
    {
       
        if (ShouldRetreat())
        {
            ai.StateMachine.ChangeState(ai.FleeState);
            return;
        }

        if (ai.CurrentTarget == null) return;

   
        float distanceToTarget = Vector3.Distance(
            enemy.transform.position, ai.CurrentTarget.transform.position);

        if (distanceToTarget > enemy.GetStats().AttackRange.Value)
            ai.Agent.SetDestination(ai.CurrentTarget.transform.position);
        else
            ai.Agent.ResetPath();

        heavyAttackTimer += Time.deltaTime;
        if (heavyAttackTimer >= heavyAttackCooldown)
        {
            heavyAttackTimer = 0f;
            enemy.Attack(ai.CurrentTarget); 
            return;
        }

        base.OnUpdate();
    }

    public override void OnExit()
    {
        ai.Agent.angularSpeed = 120f;
    }

    protected override void PerformAttack()
    {
        enemy.Attack(ai.CurrentTarget);
    }

    private bool ShouldRetreat()
    {
        if (ai is not OrcAIController orc) return false;
        var rc = enemy.GetResourceController();
        if (rc == null) return false;
        float maxHealth = enemy.GetStats().MaxHealth.Value;
        return rc.CurrentHealth / maxHealth <= orc.RetreatThreshold;
    }
}