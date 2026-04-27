using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class DemiGodPhase2State : EnemyStateAttack
{
    private DemiGodAIController demiGodAI;

    private float preferredDistance = 12f;

    private float specialAttackCooldown = 10f;
    private float specialAttackTimer;

    private float summonTimer;

    private float heavyAttackCooldown = 3f;
    private float heavyAttackTimer;

    public DemiGodPhase2State(Enemy enemy, DemiGodAIController ai)
        : base(enemy, ai, attackCooldown: 2.5f)
    {
        demiGodAI = ai;
    }

    public override void OnEnter()
    {
        base.OnEnter();

        specialAttackTimer = 0f;
        summonTimer = 0f;
        heavyAttackTimer = heavyAttackCooldown;

        Debug.Log("[DemiGod] Fase 2 — Magia + invocaciones");
    }

    public override void OnUpdate()
    {
        if (ai.CurrentTarget == null) return;

        float dist = Vector3.Distance(
            enemy.transform.position,
            ai.CurrentTarget.transform.position);

        heavyAttackTimer += Time.deltaTime;

        if (dist <= demiGodAI.TooCloseDistance && heavyAttackTimer >= heavyAttackCooldown)
        {
            heavyAttackTimer = 0f;

            Debug.Log("[DemiGod] HEAVY DEFENSIVO");

            enemy.OnAttackPressed();
            return;
        }

        MaintainDistance(dist);

        specialAttackTimer += Time.deltaTime;
        if (specialAttackTimer >= specialAttackCooldown)
        {
            specialAttackTimer = 0f;

            Debug.Log("[DemiGod] SPECIAL");

            enemy.SpecialAttack();
        }

        summonTimer += Time.deltaTime;
        if (summonTimer >= demiGodAI.Phase2SummonCooldown)
        {
            summonTimer = 0f;
            demiGodAI.SummonAllies(demiGodAI.GoblinPrefab, 3, 4f);
        }

        base.OnUpdate();
    }

    protected override void PerformAttack()
    {
        if (demiGodAI.SpellPrefab == null)
        {
            enemy.OnAttackPressed();
            return;
        }

        Vector3 spawnPos = enemy.transform.position + Vector3.up * 2f;
        Vector3 direction = (ai.CurrentTarget.transform.position + Vector3.up - spawnPos).normalized;

        var go = Object.Instantiate(
            demiGodAI.SpellPrefab,
            spawnPos,
            Quaternion.LookRotation(direction));

        var netObj = go.GetComponent<NetworkObject>();

        if (netObj != null && NetworkManager.Singleton.IsServer)
        {
            netObj.Spawn();

            var proj = go.GetComponent<NetworkProjectile>();
            if (proj != null)
            {
                float dmg = enemy.GetStats().Attack.Value;

                proj.Initialize(enemy, dmg, direction);

                Debug.Log($"[DemiGod] PROJECTILE -> {ai.CurrentTarget.name} DMG:{dmg}");
            }
        }
        else
        {
            Debug.LogError("[DemiGod] SpellPrefab sin NetworkObject");
        }
    }

    private void MaintainDistance(float currentDistance)
    {
        if (currentDistance < preferredDistance * 0.6f)
        {
            Vector3 away = (enemy.transform.position - ai.CurrentTarget.transform.position).normalized;
            Vector3 pos = enemy.transform.position + away * preferredDistance;

            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, preferredDistance, NavMesh.AllAreas))
                ai.Agent.SetDestination(hit.position);
        }
        else if (currentDistance > preferredDistance * 1.4f)
        {
            ai.Agent.SetDestination(ai.CurrentTarget.transform.position);
        }
        else
        {
            ai.Agent.ResetPath();
        }
    }
}