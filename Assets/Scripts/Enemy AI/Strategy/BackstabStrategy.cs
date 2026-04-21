using UnityEngine;
using UnityEngine.AI;

public class BackstabStrategy : IEnemyStrategy
{
    private float updateInterval = 1.2f;
    private float timer;

    public void OnEnter(Enemy enemy, EnemyAIController ai)
    {
        timer = updateInterval;
        Debug.Log($"[{enemy.name}] Estrategia: Backstab");
    }

    public void OnUpdate(Enemy enemy, EnemyAIController ai)
    {
        if (ai.CurrentTarget == null) return;

        timer += Time.deltaTime;
        if (timer < updateInterval) return;
        timer = 0f;

        Vector3 behindTarget = CalculateBehindPosition(enemy, ai.CurrentTarget);

        if (NavMesh.SamplePosition(behindTarget, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            ai.Agent.SetDestination(hit.position);
    }

    public void OnExit(Enemy enemy, EnemyAIController ai) { }

    private Vector3 CalculateBehindPosition(Enemy enemy, Character target)
    {
        float attackRange = enemy.GetStats().AttackRange.Value;

       
        return target.transform.position - target.transform.forward * attackRange;
    }
}