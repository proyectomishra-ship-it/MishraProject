using UnityEngine;

public abstract class EnemyStateAttack : EnemyState
{
    protected float attackCooldown;
    protected float attackTimer;

    protected EnemyStateAttack(
        Enemy enemy,
        EnemyAIController ai,
        float attackCooldown)
        : base(enemy, ai)
    {
        this.attackCooldown = attackCooldown;
    }

    // =========================
    // ENTER
    // =========================

    public override void OnEnter()
    {
        if (!enemy.IsServer)
            return;

        attackTimer = attackCooldown;

        ai.Agent?.ResetPath();

        Debug.Log($"[{enemy.name}] Attack ENTER");
    }

    // =========================
    // UPDATE
    // =========================

    public override void OnUpdate()
    {
        if (!enemy.IsServer)
            return;

        // =========================
        // VALIDACIÓN TARGET
        // =========================

        if (!IsTargetValid())
        {
            ai.SetTarget(null);

            ai.StateMachine.ChangeState(
                ai.HasPatrolPoints
                    ? ai.PatrolState
                    : ai.IdleState);

            return;
        }

        // =========================
        // FLEE
        // =========================

        if (ai.ShouldFlee &&
            ai.FleeState != null)
        {
            ai.StateMachine.ChangeState(
                ai.FleeState);

            return;
        }

        // =========================
        // TARGET SYNC
        // =========================

        enemy.GetTargetingController()
            ?.ForceTarget(ai.CurrentTarget);

        // =========================
        // RANGE CHECK
        // =========================

        if (!IsTargetInAttackRange())
        {
            ai.StateMachine.ChangeState(
                ai.ChaseState);

            return;
        }

        // =========================
        // ROTATION
        // =========================

        FaceTarget();

        // =========================
        // ATTACK TIMER
        // =========================

        attackTimer += Time.deltaTime;

        if (attackTimer < attackCooldown)
            return;

        attackTimer = 0f;

        PerformAttack();
    }

    // =========================
    // VALID TARGET
    // =========================

    protected virtual bool IsTargetValid()
    {
        if (ai.CurrentTarget == null)
            return false;

        if (!ai.CurrentTarget.IsSpawned)
            return false;

        ResourceController rc =
            ai.CurrentTarget.GetResourceController();

        if (rc == null)
            return false;

        return rc.CurrentHealth > 0f;
    }

    // =========================
    // RANGE
    // =========================

    protected virtual bool IsTargetInAttackRange()
    {
        if (ai.CurrentTarget == null)
            return false;

        float distance =
            Vector3.Distance(
                enemy.transform.position,
                ai.CurrentTarget.transform.position);

        return distance <=
               enemy.GetStats()
                    .AttackRange.Value;
    }

    // =========================
    // ROTATION
    // =========================

    protected virtual void FaceTarget()
    {
        if (ai.CurrentTarget == null)
            return;

        Vector3 direction =
            ai.CurrentTarget.transform.position -
            enemy.transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized);

        enemy.transform.rotation =
            Quaternion.Slerp(
                enemy.transform.rotation,
                targetRotation,
                Time.deltaTime * 10f);
    }

    // =========================
    // ATTACK
    // =========================

    protected abstract void PerformAttack();
}