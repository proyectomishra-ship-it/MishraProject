using UnityEngine;
using UnityEngine.AI;

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
        Debug.Log("[DemiGod] Fase 2 — Magia + Aliados defensivos.");
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
            enemy.Attack(ai.CurrentTarget);
            Debug.Log("[DemiGod] ¡Heavy defensivo! Jugador demasiado cerca.");
            return;
        }

       
        MaintainDistance(dist);

    
        specialAttackTimer += Time.deltaTime;
        if (specialAttackTimer >= specialAttackCooldown)
        {
            specialAttackTimer = 0f;
            enemy.SpecialAttack();
            Debug.Log("[DemiGod] Special Attack mágico!");
        }

        summonTimer += Time.deltaTime;
        if (summonTimer >= demiGodAI.Phase2SummonCooldown)
        {
            summonTimer = 0f;
            demiGodAI.SummonAllies(demiGodAI.GoblinPrefab, 3, spawnRadius: 4f);
        }


        base.OnUpdate();
    }

    protected override void PerformAttack()
    {
        if (demiGodAI.SpellPrefab == null)
        {
            enemy.Attack(ai.CurrentTarget);
            return;
        }

        Vector3 spawnPos = enemy.transform.position + Vector3.up * 2f;
        Vector3 direction = (ai.CurrentTarget.transform.position
            + Vector3.up - spawnPos).normalized;

        UnityEngine.Object.Instantiate(
            demiGodAI.SpellPrefab, spawnPos,
            Quaternion.LookRotation(direction));

        Debug.Log($"[DemiGod] Hechizo lanzado.");
    }

    private void MaintainDistance(float currentDistance)
    {
        if (currentDistance < preferredDistance * 0.6f)
        {
           
            Vector3 away = (enemy.transform.position
                - ai.CurrentTarget.transform.position).normalized;
            Vector3 retreatPos = enemy.transform.position + away * preferredDistance;

            if (NavMesh.SamplePosition(retreatPos, out NavMeshHit hit,
                preferredDistance, NavMesh.AllAreas))
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