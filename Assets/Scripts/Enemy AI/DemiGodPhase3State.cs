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

        combat = enemy.GetComponent<CombatController>();

        specialAttackTimer =
            specialAttackCooldown * 0.7f;

        heavyAttackTimer =
            heavyAttackCooldown;

        summonTimer = 0f;

        Debug.Log("[DemiGod] FASE 3 FINAL");
    }

    public override void OnUpdate()
    {
        if (ai.CurrentTarget == null)
            return;

        float dist = Vector3.Distance(
            enemy.transform.position,
            ai.CurrentTarget.transform.position);

        // =========================
        // MELEE DEFENSIVO
        // =========================

        heavyAttackTimer += Time.deltaTime;

        if (dist <= demiGodAI.TooCloseDistance &&
            heavyAttackTimer >= heavyAttackCooldown)
        {
            heavyAttackTimer = 0f;

            Debug.Log("[DemiGod] HEAVY DEFENSIVO");

            combat?.AttackDirect(true);

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

            combat?.SpecialAttackDirect();

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

        base.OnUpdate();
    }

    protected override bool IsTargetInAttackRange()
    {
        return ai.CurrentTarget != null;
    }

    protected override void PerformAttack()
    {
        if (ai.CurrentTarget == null)
            return;

        if (demiGodAI.SpellPrefab == null)
        {
            combat?.AttackDirect();
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

        var go = Object.Instantiate(
            demiGodAI.SpellPrefab,
            spawnPos,
            Quaternion.LookRotation(direction));

        var netObj = go.GetComponent<NetworkObject>();

        if (netObj != null &&
            NetworkManager.Singleton.IsServer)
        {
            netObj.Spawn();

            var proj =
                go.GetComponent<NetworkProjectile>();

            if (proj != null)
            {
                float dmg =
                    enemy.GetStats().Attack.Value;

                proj.Initialize(
                    enemy,
                    dmg,
                    direction);

                Debug.Log(
                    $"[DemiGod] PROJECTILE -> {ai.CurrentTarget.name}");
            }
        }
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
}