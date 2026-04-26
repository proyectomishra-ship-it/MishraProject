using UnityEngine;
using Unity.Netcode;

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
        if (!enemy.IsServer) return;

        attackTimer = attackCooldown;

        Debug.Log($"[{enemy.name}] Attack State ENTER");
    }

    public override void OnUpdate()
    {

        if (!enemy.IsServer) return;

        if (ai.ShouldFlee)
        {
            ai.StateMachine.ChangeState(ai.FleeState);
            return;
        }

        if (ai.CurrentTarget == null)
        {
            Debug.Log($"[{enemy.name}] Sin target -> Idle");
            ai.StateMachine.ChangeState(ai.IdleState);
            return;
        }


        enemy.GetComponent<TargetingController>()?.ForceTarget(ai.CurrentTarget);

        float distanceToTarget = Vector3.Distance(
            enemy.transform.position,
            ai.CurrentTarget.transform.position
        );

        float attackRange = enemy.GetStats().AttackRange.Value;

        // =========================
        // FUERA DE RANGO
        // =========================

        if (distanceToTarget > attackRange)
        {
            Debug.Log($"[{enemy.name}] Target fuera de rango ({distanceToTarget:F2} > {attackRange:F2}) -> Chase");

            ai.StateMachine.ChangeState(ai.ChaseState);
            return;
        }

        // =========================
        // MIRAR AL TARGET
        // =========================

        FaceTarget();

        // =========================
        // TIMER DE ATAQUE
        // =========================

        attackTimer += Time.deltaTime;

        if (attackTimer >= attackCooldown)
        {
            attackTimer = 0f;

            Debug.Log($"[{enemy.name}] Ejecutando ataque");

            PerformAttack();
        }
    }

    private void FaceTarget()
    {
        Vector3 direction = (
            ai.CurrentTarget.transform.position
            - enemy.transform.position
        ).normalized;

        direction.y = 0f;

        if (direction != Vector3.zero)
        {
            enemy.transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    protected abstract void PerformAttack();
}