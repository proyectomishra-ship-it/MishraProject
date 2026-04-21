using UnityEngine;

public abstract class EnemyStateAttack : EnemyState
{
    protected float attackCooldown;
    protected float attackTimer;

    public EnemyStateAttack(Enemy enemy, EnemyAIController ai, float attackCooldown)
        : base(enemy, ai)
    {
        this.attackCooldown = attackCooldown;
    }

    public override void OnEnter()
    {
        attackTimer = attackCooldown;
        Debug.Log($"[{enemy.name}] Attack");
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
            ai.StateMachine.ChangeState(ai.IdleState);
            return;
        }

        float distanceToTarget = Vector3.Distance(
            enemy.transform.position,
            ai.CurrentTarget.transform.position);

      
        if (distanceToTarget > enemy.GetStats().AttackRange.Value)
        {
            ai.StateMachine.ChangeState(ai.ChaseState);
            return;
        }

     
        FaceTarget();

      
        attackTimer += Time.deltaTime;
        if (attackTimer >= attackCooldown)
        {
            attackTimer = 0f;
            PerformAttack();
        }
    }

   
    private void FaceTarget()
    {
        Vector3 direction = (ai.CurrentTarget.transform.position
            - enemy.transform.position).normalized;
        direction.y = 0f;

        if (direction != Vector3.zero)
            enemy.transform.rotation = Quaternion.LookRotation(direction);
    }

  
    protected abstract void PerformAttack();
}