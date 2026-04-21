using UnityEngine;
using UnityEngine.AI;

public class DemiGodPhase3State : EnemyStateAttack
{
    private DemiGodAIController demiGodAI;

    private float specialAttackCooldown = 5f;
    private float specialAttackTimer;

   
    private float heavyAttackCooldown = 2f;
    private float heavyAttackTimer;


    private float summonTimer;

  
    private float preferredDistance = 8f;

    public DemiGodPhase3State(Enemy enemy, DemiGodAIController ai)
        : base(enemy, ai, attackCooldown: 2f)
    {
        demiGodAI = ai;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        
        specialAttackTimer = specialAttackCooldown * 0.7f;
        heavyAttackTimer = heavyAttackCooldown;
        summonTimer = 0f;
        Debug.Log("[DemiGod] ¡FASE 3 — FORMA FINAL!");
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
            Debug.Log("[DemiGod] Heavy defensivo fase 3.");
            return;
        }

        
        specialAttackTimer += Time.deltaTime;
        if (specialAttackTimer >= specialAttackCooldown)
        {
            specialAttackTimer = 0f;
            enemy.SpecialAttack();
            Debug.Log("[DemiGod] ¡Special Attack! (Fase 3)");
            return;
        }

        
        summonTimer += Time.deltaTime;
        if (summonTimer >= demiGodAI.Phase3SummonCooldown)
        {
            summonTimer = 0f;
            demiGodAI.SummonAllies(demiGodAI.OrcPrefab, 1, spawnRadius: 5f);
            demiGodAI.SummonAllies(demiGodAI.GoblinPrefab, 3, spawnRadius: 4f);
        }

       
        if (dist < preferredDistance * 0.5f)
        {
            Vector3 away = (enemy.transform.position
                - ai.CurrentTarget.transform.position).normalized;
            Vector3 retreatPos = enemy.transform.position + away * preferredDistance;

            if (NavMesh.SamplePosition(retreatPos, out NavMeshHit hit,
                preferredDistance, NavMesh.AllAreas))
                ai.Agent.SetDestination(hit.position);
        }
        else
        {
            ai.Agent.ResetPath();
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
    }
}