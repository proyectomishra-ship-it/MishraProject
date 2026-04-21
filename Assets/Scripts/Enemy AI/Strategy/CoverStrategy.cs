using UnityEngine;
using UnityEngine.AI;

public class CoverStrategy : IEnemyStrategy
{
    private float coverSearchRadius = 15f;
    private float updateInterval = 2f;
    private float timer;
    private Vector3 coverPosition;
    private bool hasCover;

    public void OnEnter(Enemy enemy, EnemyAIController ai)
    {
        timer = updateInterval;
        hasCover = false;
        Debug.Log($"[{enemy.name}] Estrategia: Cobertura");
        FindCover(enemy, ai);
    }

    public void OnUpdate(Enemy enemy, EnemyAIController ai)
    {
        if (ai.CurrentTarget == null) return;

        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;
            FindCover(enemy, ai);
        }

        if (hasCover && ai.Agent.remainingDistance <= 0.5f)
            ai.Agent.ResetPath();
    }

    public void OnExit(Enemy enemy, EnemyAIController ai)
    {
        hasCover = false;
    }

    private void FindCover(Enemy enemy, EnemyAIController ai)
    {
        if (ai.CurrentTarget == null) return;

        Vector3 playerPos = ai.CurrentTarget.transform.position;
        Vector3 enemyPos = enemy.transform.position;

        
        Vector3 dirFromPlayer = (enemyPos - playerPos).normalized;
        Vector3 candidatePos = enemyPos + dirFromPlayer * coverSearchRadius * 0.5f;

       
        Vector3 lateral = Vector3.Cross(dirFromPlayer, Vector3.up)
            * Random.Range(-coverSearchRadius * 0.3f, coverSearchRadius * 0.3f);
        candidatePos += lateral;

        if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit,
            coverSearchRadius, NavMesh.AllAreas))
        {
            coverPosition = hit.position;
            hasCover = true;
            ai.Agent.SetDestination(coverPosition);
        }
    }
}