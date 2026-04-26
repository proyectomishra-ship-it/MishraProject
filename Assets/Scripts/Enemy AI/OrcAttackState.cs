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

        Debug.Log($"[Orc] {enemy.name} entra en estado ATTACK (Tank)");
    }

    public override void OnUpdate()
    {
        if (ShouldRetreat())
        {
            Debug.Log($"[Orc] {enemy.name} se retira (low HP)");
            ai.StateMachine.ChangeState(ai.FleeState);
            return;
        }

        if (ai.CurrentTarget == null)
        {
            Debug.LogWarning($"[Orc] {enemy.name} sin target");
            return;
        }

        // =========================
        // POSICIONAMIENTO (TANK)
        // =========================

        Vector3 destination = CombatSlotManager.Instance != null
            ? CombatSlotManager.Instance.GetTankSlotPosition(ai, ai.CurrentTarget.transform)
            : ai.CurrentTarget.transform.position;

        float distanceToTarget = Vector3.Distance(
            enemy.transform.position,
            ai.CurrentTarget.transform.position);

        if (distanceToTarget > enemy.GetStats().AttackRange.Value)
        {
            ai.Agent.SetDestination(destination);
        }
        else
        {
            ai.Agent.ResetPath();
        }

        // =========================
        // HEAVY ATTACK (simulado)
        // =========================

        heavyAttackTimer += Time.deltaTime;

        if (heavyAttackTimer >= heavyAttackCooldown)
        {
            heavyAttackTimer = 0f;

            Debug.Log($"[Orc] {enemy.name} HEAVY ATTACK (simulado)");

       
            enemy.OnAttackPressed();

            return;
        }

        // =========================
        // ATAQUE BASE (cooldown)
        // =========================

        base.OnUpdate();
    }

    public override void OnExit()
    {
        if (ai.CurrentTarget != null)
            CombatSlotManager.Instance?.RemoveTank(ai, ai.CurrentTarget.transform);

        ai.Agent.angularSpeed = 120f;

        Debug.Log($"[Orc] {enemy.name} sale de ATTACK");
    }

    protected override void PerformAttack()
    {
        if (ai.CurrentTarget == null)
        {
            Debug.LogWarning($"[Orc] PerformAttack sin target");
            return;
        }

        Debug.Log($"[Orc] {enemy.name} ATTACK a {ai.CurrentTarget.name}");


        enemy.OnAttackPressed();
    }

    private bool ShouldRetreat()
    {
        if (ai is not OrcAIController orc) return false;

        var rc = enemy.GetResourceController();
        if (rc == null) return false;

        float maxHealth = enemy.GetStats().MaxHealth.Value;

        float hpPercent = rc.CurrentHealth / maxHealth;

        if (hpPercent <= orc.RetreatThreshold)
        {
            Debug.Log($"[Orc] HP bajo ({hpPercent:P0}) -> RETREAT");
            return true;
        }

        return false;
    }
}