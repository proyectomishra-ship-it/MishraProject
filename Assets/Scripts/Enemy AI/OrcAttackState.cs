using UnityEngine;
using UnityEngine.AI;

public class OrcAttackState : EnemyStateAttack
{
    private float heavyAttackCooldown = 4f;

    private float heavyAttackTimer;

    public OrcAttackState(
        Enemy enemy,
        EnemyAIController ai)
        : base(enemy, ai, attackCooldown: 1.8f)
    {
        heavyAttackTimer =
            heavyAttackCooldown;
    }

    public override void OnEnter()
    {
        base.OnEnter();

        if (!enemy.IsServer)
            return;

        heavyAttackTimer = 0f;

        ai.Agent.angularSpeed = 80f;

        ai.Agent.speed =
            enemy.GetStats().Speed.Value;

        Debug.Log(
            $"[Orc] {enemy.name} entra en ATTACK");
    }

    public override void OnUpdate()
    {
        if (!enemy.IsServer)
            return;

        if (ShouldRetreat())
        {
            Debug.Log(
                $"[Orc] {enemy.name} RETREAT");

            ai.StateMachine.ChangeState(
                ai.FleeState);

            return;
        }

        if (ai.CurrentTarget == null)
        {
            Debug.LogWarning(
                $"[Orc] {enemy.name} sin target");

            return;
        }

        enemy.GetTargetingController()
            ?.ForceTarget(ai.CurrentTarget);

        Vector3 destination =
            CombatSlotManager.Instance != null
            ? CombatSlotManager.Instance
                .GetTankSlotPosition(
                    ai,
                    ai.CurrentTarget.transform)
            : ai.CurrentTarget.transform.position;

        float distanceToTarget =
            Vector3.Distance(
                enemy.transform.position,
                ai.CurrentTarget.transform.position);

        if (distanceToTarget >
            enemy.GetStats().AttackRange.Value)
        {
            ai.Agent.SetDestination(destination);
        }
        else
        {
            ai.Agent.ResetPath();
        }

        heavyAttackTimer += Time.deltaTime;

        if (heavyAttackTimer >= heavyAttackCooldown)
        {
            heavyAttackTimer = 0f;

            Debug.Log(
                $"[Orc] {enemy.name} HEAVY ATTACK");

            enemy.OnAttackPressed();

            enemy.OnAttackHeld();

            return;
        }

        base.OnUpdate();
    }

    public override void OnExit()
    {
        if (!enemy.IsServer)
            return;

        if (ai.CurrentTarget != null)
        {
            CombatSlotManager.Instance?.RemoveTank(
                ai,
                ai.CurrentTarget.transform);
        }

        ai.Agent.angularSpeed = 120f;

        Debug.Log(
            $"[Orc] {enemy.name} sale de ATTACK");
    }

    protected override void PerformAttack()
    {
        if (!enemy.IsServer)
            return;

        if (ai.CurrentTarget == null)
        {
            Debug.LogWarning(
                $"[Orc] PerformAttack sin target");

            return;
        }

        enemy.GetTargetingController()
            ?.ForceTarget(ai.CurrentTarget);

        Debug.Log(
            $"[Orc] {enemy.name} LIGHT ATTACK");

        enemy.OnAttackPressed();

        enemy.OnAttackReleased();
    }

    private bool ShouldRetreat()
    {
        if (ai is not OrcAIController orc)
            return false;

        ResourceController rc =
            enemy.GetResourceController();

        if (rc == null)
            return false;

        float hpPercent =
            rc.CurrentHealth /
            enemy.GetStats().MaxHealth.Value;

        if (hpPercent <= orc.RetreatThreshold)
        {
            Debug.Log(
                $"[Orc] HP bajo ({hpPercent:P0})");

            return true;
        }

        return false;
    }
}