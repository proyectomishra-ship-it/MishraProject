using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class DemiGodPhase3State : EnemyStateAttack
{
    private DemiGodAIController demiGodAI;

    private CombatController combat;

    private float specialAttackCooldown = 5f;
    private float specialAttackTimer;

    private float heavyAttackCooldown = 2f;
    private float heavyAttackTimer;

    private float summonTimer;

    private float preferredDistance = 8f;

    private float heavyHoldTimer;
    private bool isChargingHeavy;

    public DemiGodPhase3State(
        Enemy enemy,
        DemiGodAIController ai)
        : base(enemy, ai, attackCooldown: 2f)
    {
        demiGodAI = ai;
    }

    public override void OnEnter()
    {
        base.OnEnter();

        if (!enemy.IsServer)
            return;

        combat =
            enemy.GetComponent<CombatController>();

        specialAttackTimer =
            specialAttackCooldown * 0.7f;

        heavyAttackTimer =
            heavyAttackCooldown;

        summonTimer = 0f;

        heavyHoldTimer = 0f;
        isChargingHeavy = false;

        Debug.Log("[DemiGod] FASE 3 FINAL");
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
        // HEAVY CHARGE UPDATE
        // =========================

        UpdateHeavyCharge();

        // =========================
        // HEAVY DEFENSIVE
        // =========================

        heavyAttackTimer += Time.deltaTime;

        if (!isChargingHeavy &&
            dist <= demiGodAI.TooCloseDistance &&
            heavyAttackTimer >= heavyAttackCooldown)
        {
            heavyAttackTimer = 0f;

            Debug.Log("[DemiGod] HEAVY DEFENSIVO");

            StartHeavyAttack();

            return;
        }

        // =========================
        // SPECIAL
        // =========================

        specialAttackTimer += Time.deltaTime;

        if (specialAttackTimer >= specialAttackCooldown)
        {
            specialAttackTimer = 0f;

            Debug.Log("[DemiGod] SPECIAL FASE 3");

            combat?.ExecuteSpecialAttack();

            return;
        }

        // =========================
        // SUMMON
        // =========================

        summonTimer += Time.deltaTime;

        if (summonTimer >= demiGodAI.Phase3SummonCooldown)
        {
            summonTimer = 0f;

            demiGodAI.SummonAllies(
                demiGodAI.OrcPrefab,
                1,
                5f);

            demiGodAI.SummonAllies(
                demiGodAI.GoblinPrefab,
                3,
                4f);
        }

        // =========================
        // MOVEMENT
        // =========================

        MaintainDistance(dist);

        // =========================
        // BASE ATTACK
        // =========================

        base.OnUpdate();
    }

    protected override bool IsTargetInAttackRange()
    {
        return ai.CurrentTarget != null;
    }

    protected override void PerformAttack()
    {
        if (!enemy.IsServer)
            return;

        if (ai.CurrentTarget == null)
            return;

        if (demiGodAI.SpellPrefab == null)
        {
            combat?.ExecuteAttack();

            return;
        }

        Vector3 spawnPos =
            enemy.transform.position + Vector3.up * 2f;

        Vector3 direction =
            (
                ai.CurrentTarget.transform.position
                + Vector3.up
                - spawnPos
            ).normalized;

        GameObject go =
            Object.Instantiate(
                demiGodAI.SpellPrefab,
                spawnPos,
                Quaternion.LookRotation(direction));

        NetworkObject netObj =
            go.GetComponent<NetworkObject>();

        NetworkProjectile projectile =
            go.GetComponent<NetworkProjectile>();

        if (netObj == null || projectile == null)
        {
            Debug.LogError(
                "[DemiGod] SpellPrefab mal configurado");

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

        netObj.Spawn();

        projectile.Initialize(
            attackData,
            direction,
            18f);

        Debug.Log(
            $"[DemiGod] PROJECTILE -> {ai.CurrentTarget.name}");
    }

    private void MaintainDistance(float currentDistance)
    {
        if (ai.CurrentTarget == null)
            return;

        if (currentDistance < preferredDistance * 0.5f)
        {
            Vector3 away =
                (
                    enemy.transform.position
                    - ai.CurrentTarget.transform.position
                ).normalized;

            Vector3 pos =
                enemy.transform.position
                + away * preferredDistance;

            if (NavMesh.SamplePosition(
                pos,
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
    // HEAVY ATTACK SYSTEM
    // =========================

    private void StartHeavyAttack()
    {
        if (combat == null)
            return;

        enemy.OnAttackPressed();

        heavyHoldTimer = 0f;

        isChargingHeavy = true;
    }

    private void UpdateHeavyCharge()
    {
        if (!isChargingHeavy)
            return;

        heavyHoldTimer += Time.deltaTime;

        enemy.OnAttackHeld();

        if (heavyHoldTimer >= 0.55f)
        {
            enemy.OnAttackReleased();

            isChargingHeavy = false;
        }
    }
}