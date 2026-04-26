using UnityEngine;

public class DemiGodPhase1State : EnemyStateAttack
{
    private float heavyAttackCooldown = 5f;
    private float heavyAttackTimer;

    private float specialAttackCooldown = 15f;
    private float specialAttackTimer;

    public DemiGodPhase1State(Enemy enemy, EnemyAIController ai)
        : base(enemy, ai, attackCooldown: 1.5f)
    {
    }

    public override void OnEnter()
    {
        base.OnEnter();

        heavyAttackTimer = 0f;
        specialAttackTimer = 0f;

        ai.Agent.speed = enemy.GetStats().Speed.Value;

        Debug.Log("[DemiGod] Fase 1 — Melee agresivo");
    }

    public override void OnUpdate()
    {
        if (ai.CurrentTarget == null) return;

        float dist = Vector3.Distance(
            enemy.transform.position,
            ai.CurrentTarget.transform.position);

        if (dist > enemy.GetStats().AttackRange.Value)
            ai.Agent.SetDestination(ai.CurrentTarget.transform.position);
        else
            ai.Agent.ResetPath();

        heavyAttackTimer += Time.deltaTime;
        specialAttackTimer += Time.deltaTime;

        if (heavyAttackTimer >= heavyAttackCooldown)
        {
            heavyAttackTimer = 0f;

            Debug.Log("[DemiGod] HEAVY ATTACK (simulado)");

            enemy.OnAttackPressed();
            return;
        }

        if (specialAttackTimer >= specialAttackCooldown)
        {
            specialAttackTimer = 0f;

            Debug.Log("[DemiGod] SPECIAL ATTACK");

            enemy.SpecialAttack();
            return;
        }

        base.OnUpdate();
    }

    protected override void PerformAttack()
    {
        if (ai.CurrentTarget == null) return;

        Debug.Log("[DemiGod] ATTACK base");

        enemy.OnAttackPressed();
    }
}