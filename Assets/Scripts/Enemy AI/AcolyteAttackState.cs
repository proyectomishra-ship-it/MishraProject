using UnityEngine;
using UnityEngine.AI;

public class AcolyteAttackState : EnemyStateAttack
{
    private AcolyteAIController acolyteAI;
    private float specialAttackCooldown = 8f;
    private float specialAttackTimer;
    private float repositionTimer;
    private float repositionInterval = 2f;

    public AcolyteAttackState(Enemy enemy, AcolyteAIController ai)
        : base(enemy, ai, attackCooldown: 2.5f)
    {
        acolyteAI = ai;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        specialAttackTimer = specialAttackCooldown;
        repositionTimer = repositionInterval;
        Debug.Log($"[Acolyte] Preparando hechizo.");
    }

    public override void OnUpdate()
    {
        if (ai.ShouldFlee)
        {
            ai.StateMachine.ChangeState(ai.FleeState);
            return;
        }

        if (ai.CurrentTarget == null) return;

      
        repositionTimer += Time.deltaTime;
        if (repositionTimer >= repositionInterval)
        {
            repositionTimer = 0f;
            MaintainDistance();
        }

       
        specialAttackTimer += Time.deltaTime;
        if (specialAttackTimer >= specialAttackCooldown)
        {
            specialAttackTimer = 0f;
            enemy.SpecialAttack();
        }

        base.OnUpdate();
    }

    protected override void PerformAttack()
    {
        if (acolyteAI.SpellPrefab == null)
        {
            enemy.Attack(ai.CurrentTarget);
            return;
        }

        Vector3 spawnPos = enemy.transform.position + Vector3.up * 1.5f;
        Vector3 direction = (ai.CurrentTarget.transform.position
            + Vector3.up - spawnPos).normalized;

        UnityEngine.Object.Instantiate(
            acolyteAI.SpellPrefab, spawnPos, Quaternion.LookRotation(direction));

        Debug.Log($"[Acolyte] Hechizo lanzado hacia {ai.CurrentTarget.name}");
    }

    private void MaintainDistance()
    {
        float dist = Vector3.Distance(
            enemy.transform.position, ai.CurrentTarget.transform.position);

        if (dist < acolyteAI.PreferredCombatDistance * 0.5f)
        {
            
            Vector3 away = (enemy.transform.position
                - ai.CurrentTarget.transform.position).normalized;
            Vector3 lateral = Vector3.Cross(away, Vector3.up)
                * Random.Range(-3f, 3f);
            Vector3 retreatPos = enemy.transform.position
                + (away + lateral).normalized * acolyteAI.PreferredCombatDistance;

            if (NavMesh.SamplePosition(retreatPos, out NavMeshHit hit,
                acolyteAI.PreferredCombatDistance, NavMesh.AllAreas))
                ai.Agent.SetDestination(hit.position);
        }
        else if (dist > acolyteAI.PreferredCombatDistance * 1.5f)
        {
            
            ai.Agent.SetDestination(ai.CurrentTarget.transform.position);
        }
        else
        {
            
            Vector3 lateral = enemy.transform.right
                * (Mathf.Sin(Time.time * 0.8f) * 2f);
            Vector3 strafePos = enemy.transform.position + lateral;

            if (NavMesh.SamplePosition(strafePos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                ai.Agent.SetDestination(hit.position);
        }
    }
}