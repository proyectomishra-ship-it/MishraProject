using UnityEngine;
using Unity.Netcode;

public abstract class EnemyStateAttack : EnemyState
{
    protected float attackCooldown;
    protected float attackTimer;

    public EnemyStateAttack(
        Enemy enemy,
        EnemyAIController ai,
        float attackCooldown)
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

        // =========================
        // FLEE
        // =========================

        if (ai.ShouldFlee && ai.FleeState != null)
        {
            ai.StateMachine.ChangeState(ai.FleeState);
            return;
        }

        // =========================
        // TARGET
        // =========================

        if (ai.CurrentTarget == null)
        {
            Debug.Log($"[{enemy.name}] Sin target -> Idle");

            ai.StateMachine.ChangeState(ai.IdleState);

            return;
        }

        // =========================
        // FORCE TARGET
        // =========================

        enemy.GetComponent<TargetingController>()
            ?.ForceTarget(ai.CurrentTarget);

        // =========================
        // VALIDACIÓN DE RANGO
        // =========================

        if (!IsTargetInAttackRange())
        {
            Debug.Log(
                $"[{enemy.name}] Target fuera de rango -> Chase");

            ai.StateMachine.ChangeState(ai.ChaseState);

            return;
        }

        // =========================
        // MIRAR TARGET
        // =========================

        FaceTarget();

        // =========================
        // ATTACK TIMER
        // =========================

        attackTimer += Time.deltaTime;

        if (attackTimer >= attackCooldown)
        {
            attackTimer = 0f;

            Debug.Log($"[{enemy.name}] Ejecutando ataque");

            PerformAttack();
        }
    }

    // =========================
    // RANGE VALIDATION
    // =========================

    protected virtual bool IsTargetInAttackRange()
    {
        if (ai.CurrentTarget == null)
            return false;

        float distance = Vector3.Distance(
            enemy.transform.position,
            ai.CurrentTarget.transform.position);

        float attackRange =
            enemy.GetStats().AttackRange.Value;

        return distance <= attackRange;
    }

    // =========================
    // FACE TARGET
    // =========================

    private void FaceTarget()
    {
        if (ai.CurrentTarget == null)
            return;

        Vector3 direction =
            ai.CurrentTarget.transform.position
            - enemy.transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        enemy.transform.rotation =
            Quaternion.LookRotation(direction.normalized);
    }

    // =========================
    // ATTACK
    // =========================

    protected abstract void PerformAttack();
}