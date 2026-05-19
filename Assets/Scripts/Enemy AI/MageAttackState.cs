using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class MageAttackState : EnemyStateAttack
{
    private readonly float preferredDistance;

    private readonly GameObject spellPrefab;

    private readonly IEnemyStrategy activeStrategy;

    private readonly float specialAttackCooldown;
    private readonly float heavyAttackCooldown;

    private float specialAttackTimer;
    private float heavyAttackTimer;

    private readonly bool hasSpecial;
    private readonly bool hasHeavy;

    public MageAttackState(
        Enemy enemy,
        EnemyAIController ai,
        float attackCooldown,
        float preferredDistance,
        GameObject spellPrefab,
        float heavyAttackCooldown,
        float specialAttackCooldown,
        bool hasHeavy,
        bool hasSpecial,
        IEnemyStrategy strategy = null
    ) : base(enemy, ai, attackCooldown)
    {
        this.preferredDistance = preferredDistance;
        this.spellPrefab = spellPrefab;
        this.activeStrategy = strategy;
        this.heavyAttackCooldown = heavyAttackCooldown;
        this.specialAttackCooldown = specialAttackCooldown;
        this.hasHeavy = hasHeavy;
        this.hasSpecial = hasSpecial;
    }

    public override void OnEnter()
    {
        base.OnEnter();

        if (!enemy.IsServer)
            return;

        heavyAttackTimer = heavyAttackCooldown;
        specialAttackTimer = specialAttackCooldown;

        activeStrategy?.OnEnter(enemy, ai);

        Debug.Log($"[{enemy.name}][Mage] Enter Attack");
    }

    public override void OnExit()
    {
        activeStrategy?.OnExit(enemy, ai);

        ai.Agent?.ResetPath();
    }

    public override void OnUpdate()
    {
        if (!enemy.IsServer)
            return;

        if (!IsTargetValid())
        {
            ai.SetTarget(null);

            ai.StateMachine.ChangeState(
                ai.ChaseState);

            return;
        }

        enemy.GetTargetingController()
            ?.ForceTarget(ai.CurrentTarget);

        activeStrategy?.OnUpdate(enemy, ai);

        HandlePositioning();

        HandleHeavyAttack();

        HandleSpecialAttack();

        base.OnUpdate();
    }

    // =========================
    // POSITIONING
    // =========================

    private void HandlePositioning()
    {
        if (ai.CurrentTarget == null)
            return;

        float distanceToTarget =
            Vector3.Distance(
                enemy.transform.position,
                ai.CurrentTarget.transform.position);

        if (distanceToTarget < preferredDistance * 0.6f)
        {
            Vector3 dirAway =
                (
                    enemy.transform.position -
                    ai.CurrentTarget.transform.position
                ).normalized;

            Vector3 retreatPos =
                enemy.transform.position +
                dirAway * preferredDistance;

            if (NavMesh.SamplePosition(
                retreatPos,
                out NavMeshHit hit,
                preferredDistance,
                NavMesh.AllAreas))
            {
                ai.Agent.SetDestination(hit.position);
            }
        }
        else
        {
            ai.Agent.ResetPath();
        }
    }

    // =========================
    // HEAVY
    // =========================

    private void HandleHeavyAttack()
    {
        if (!hasHeavy)
            return;

        heavyAttackTimer += Time.deltaTime;

        if (heavyAttackTimer < heavyAttackCooldown)
            return;

        heavyAttackTimer = 0f;

        Debug.Log($"[{enemy.name}] HEAVY");

        enemy.OnAttackPressed();

        enemy.OnAttackHeld();

        enemy.OnAttackReleased();
    }

    // =========================
    // SPECIAL
    // =========================

    private void HandleSpecialAttack()
    {
        if (!hasSpecial)
            return;

        specialAttackTimer += Time.deltaTime;

        if (specialAttackTimer < specialAttackCooldown)
            return;

        specialAttackTimer = 0f;

        Debug.Log($"[{enemy.name}] SPECIAL");

        CombatController combat =
            enemy.GetComponent<CombatController>();

        combat?.ExecuteSpecialAttack();
    }

    // =========================
    // ATTACK
    // =========================

    protected override void PerformAttack()
    {
        if (!enemy.IsServer)
            return;

        if (ai.CurrentTarget == null)
            return;

        enemy.GetTargetingController()
            ?.ForceTarget(ai.CurrentTarget);

        // =========================
        // FALLBACK MELEE
        // =========================

        if (spellPrefab == null)
        {
            enemy.OnAttackPressed();
            enemy.OnAttackReleased();

            return;
        }

        Vector3 spawnPos =
            enemy.transform.position +
            Vector3.up * 1.5f;

        Vector3 direction =
            (
                ai.CurrentTarget.transform.position +
                Vector3.up -
                spawnPos
            ).normalized;

        GameObject instance =
            Object.Instantiate(
                spellPrefab,
                spawnPos,
                Quaternion.LookRotation(direction));

        NetworkObject netObj =
            instance.GetComponent<NetworkObject>();

        NetworkProjectile projectile =
            instance.GetComponent<NetworkProjectile>();

        if (netObj == null || projectile == null)
        {
            Debug.LogError(
                $"[{enemy.name}] SpellPrefab mal configurado");

            Object.Destroy(instance);

            return;
        }

        AttackData attackData =
            new AttackData
            {
                Attacker = enemy,
                Target = ai.CurrentTarget,
                Damage = enemy.GetStats().Attack.Value,
                DamageType = DamageType.Magical,
                IsCritical = false,
                IsHeavy = false,
                HitPoint = spawnPos
            };

        projectile.Initialize(
            attackData,
            direction,
            14f);

        netObj.Spawn(true);

        Debug.Log(
            $"[{enemy.name}] Spell -> {ai.CurrentTarget.name}");
    }
}