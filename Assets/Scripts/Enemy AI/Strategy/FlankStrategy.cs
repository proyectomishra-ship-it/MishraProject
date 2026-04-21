using UnityEngine;
using UnityEngine.AI;

public class FlankStrategy : IEnemyStrategy
{
    private float updateInterval = 1f;
    private float timer;

    public void OnEnter(Enemy enemy, EnemyAIController ai)
    {
        timer = updateInterval;
        Debug.Log($"[{enemy.name}] Estrategia: Flanqueo");
    }

    public void OnUpdate(Enemy enemy, EnemyAIController ai)
    {
        if (ai.CurrentTarget == null) return;

        timer += Time.deltaTime;
        if (timer < updateInterval) return;
        timer = 0f;

        Vector3 flankPosition = CalculateFlankPosition(enemy, ai.CurrentTarget);

        if (NavMesh.SamplePosition(flankPosition, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            ai.Agent.SetDestination(hit.position);
    }

    public void OnExit(Enemy enemy, EnemyAIController ai) { }

    private Vector3 CalculateFlankPosition(Enemy enemy, Character target)
    {
        
        float side = Mathf.Sin(Time.time * 0.5f) > 0 ? 1f : -1f;

        Vector3 targetForward = target.transform.forward;
        Vector3 targetRight = target.transform.right;

        
        float attackRange = enemy.GetStats().AttackRange.Value;
        return target.transform.position
            + (targetForward * -0.5f + targetRight * side).normalized
            * attackRange;
    }
}
