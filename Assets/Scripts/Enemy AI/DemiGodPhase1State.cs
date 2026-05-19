using UnityEngine;

public class DemiGodPhase1State : EnemyStateAttack
{
    private CombatController combat;

    private float heavyAttackCooldown = 5f;
    private float heavyAttackTimer;

    private float specialAttackCooldown = 15f;
    private float specialAttackTimer;

    public DemiGodPhase1State(
        Enemy enemy,
        EnemyAIController ai)
        : base(enemy, ai, attackCooldown: 1.5f)
    {
    }

    public override void OnEnter()
    {
        base.OnEnter();

        if (!enemy.IsServer)
            return;

        combat =
            enemy.GetComponent<CombatController>();

        heavyAttackTimer = 0f;
        specialAttackTimer = 0f;

        ai.Agent.speed =
            enemy.GetStats().Speed.Value;

        Debug.Log("[DemiGod] Fase 1 — Melee agresivo");
    }

    public override void OnUpdate()
    {
        if (!enemy.IsServer)
            return;

        if (ai.CurrentTarget == null)
            return;

        enemy.GetTargetingController()
            ?.ForceTarget(ai.CurrentTarget);

        float dist =
            Vector3.Distance(
                enemy.transform.position,
                ai.CurrentTarget.transform.position);

        // =========================
        // MOVEMENT
        // =========================

        if (dist > enemy.GetStats().AttackRange.Value * 0.9f)
        {
            ai.Agent.SetDestination(
                ai.CurrentTarget.transform.position);
        }
        else
        {
            ai.Agent.ResetPath();
        }

        // =========================
        // TIMERS
        // =========================

        heavyAttackTimer += Time.deltaTime;
        specialAttackTimer += Time.deltaTime;

        // =========================
        // HEAVY ATTACK
        // =========================

        if (heavyAttackTimer >= heavyAttackCooldown)
        {
            heavyAttackTimer = 0f;

            Debug.Log("[DemiGod] HEAVY ATTACK");

            enemy.OnAttackPressed();

            // Simula mantener el botón
            enemy.OnAttackHeld();
            enemy.OnAttackHeld();
            enemy.OnAttackHeld();

            enemy.OnAttackReleased();

            return;
        }

        // =========================
        // SPECIAL ATTACK
        // =========================

        if (specialAttackTimer >= specialAttackCooldown)
        {
            specialAttackTimer = 0f;

            Debug.Log("[DemiGod] SPECIAL ATTACK");

            combat?.ExecuteSpecialAttack();

            return;
        }

        // =========================
        // BASE ATTACK
        // =========================

        base.OnUpdate();
    }

    protected override void PerformAttack()
    {
        if (!enemy.IsServer)
            return;

        if (combat == null)
            return;

        Debug.Log("[DemiGod] LIGHT ATTACK");

        enemy.OnAttackPressed();
        enemy.OnAttackReleased();
    }

    protected override bool IsTargetInAttackRange()
    {
        if (ai.CurrentTarget == null)
            return false;

        float distance =
            Vector3.Distance(
                enemy.transform.position,
                ai.CurrentTarget.transform.position);

        float tolerance = 1.25f;

        return distance <=
               enemy.GetStats().AttackRange.Value + tolerance;
    }
}