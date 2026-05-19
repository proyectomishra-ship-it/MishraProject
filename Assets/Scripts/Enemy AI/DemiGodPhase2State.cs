using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class DemiGodPhase2State : EnemyStateAttack
{
    private DemiGodAIController demiGodAI;

    private CombatController combat;

    private float preferredDistance = 12f;

    private float specialAttackCooldown = 10f;
    private float specialAttackTimer;

    private float summonTimer;

    private float heavyAttackCooldown = 3f;
    private float heavyAttackTimer;

    public DemiGodPhase2State(
        Enemy enemy,
        DemiGodAIController ai)
        : base(enemy, ai, attackCooldown: 2.5f)
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

        specialAttackTimer = 0f;
        summonTimer = 0f;
        heavyAttackTimer = heavyAttackCooldown;

        Debug.Log("[DemiGod] Fase 2 — Magia + invocaciones");
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
        // DEFENSIVE HEAVY
        // =========================

        heavyAttackTimer += Time.deltaTime;

        if (dist <= demiGodAI.TooCloseDistance &&
            heavyAttackTimer >= heavyAttackCooldown)
        {
            heavyAttackTimer = 0f;

            Debug.Log("[DemiGod] HEAVY DEFENSIVO");

            enemy.OnAttackPressed();

            // Simula hold attack
            enemy.OnAttackHeld();
            enemy.OnAttackHeld();
            enemy.OnAttackHeld();

            enemy.OnAttackReleased();

            return;
        }

        // =========================
        // DISTANCE
        // =========================

        MaintainDistance(dist);

        // =========================
        // SPECIAL
        // =========================

        specialAttackTimer += Time.deltaTime;

        if (specialAttackTimer >= specialAttackCooldown)
        {
            specialAttackTimer = 0f;

            Debug.Log("[DemiGod] SPECIAL");

            combat?.ExecuteSpecialAttack();

            return;
        }

        // =========================
        // SUMMON
        // =========================

        summonTimer += Time.deltaTime;

        if (summonTimer >= demiGodAI.Phase2SummonCooldown)
        {
            summonTimer = 0f;

            demiGodAI.SummonAllies(
                demiGodAI.GoblinPrefab,
                3,
                4f);
        }

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

        enemy.GetTargetingController()
            ?.ForceTarget(ai.CurrentTarget);

        // =========================
        // FALLBACK MELEE
        // =========================

        if (demiGodAI.SpellPrefab == null)
        {
            enemy.OnAttackPressed();
            enemy.OnAttackReleased();
            return;
        }

        // =========================
        // PROJECTILE
        // =========================

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

        projectile.Initialize(
            attackData,
            direction,
            14f);

        netObj.Spawn();

        Debug.Log(
            $"[DemiGod] PROJECTILE -> {ai.CurrentTarget.name}");
    }

    private void MaintainDistance(float currentDistance)
    {
        if (ai.CurrentTarget == null)
            return;

        if (currentDistance < preferredDistance * 0.6f)
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
        else if (currentDistance > preferredDistance * 1.4f)
        {
            ai.Agent.SetDestination(
                ai.CurrentTarget.transform.position);
        }
        else
        {
            ai.Agent.ResetPath();
        }
    }
}