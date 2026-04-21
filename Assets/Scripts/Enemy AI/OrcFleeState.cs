using UnityEngine;
using UnityEngine.AI;

public class OrcFleeState : EnemyStateFlee
{
    private OrcAIController orcAI;
    private float regenTimer;
    private const float regenInterval = 1f;

    public OrcFleeState(Enemy enemy, OrcAIController ai)
        : base(enemy, ai, fleeDistance: 20f)
    {
        orcAI = ai;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        regenTimer = 0f;
        Debug.Log($"[Orc] Retreat! Regenerando...");
    }

    public override void OnUpdate()
    {
        
        regenTimer += Time.deltaTime;
        if (regenTimer >= regenInterval)
        {
            regenTimer = 0f;
            enemy.Heal(orcAI.RegenPerSecond * regenInterval);
        }

      
        if (ShouldRally())
        {
            Debug.Log($"[Orc] Rally! Volviendo a la batalla.");
            ai.SetAlerted(true);
            ai.StateMachine.ChangeState(ai.ChaseState);
            return;
        }

        base.OnUpdate();
    }

    private bool ShouldRally()
    {
        var rc = enemy.GetResourceController();
        if (rc == null) return false;
        float maxHealth = enemy.GetStats().MaxHealth.Value;
        return rc.CurrentHealth / maxHealth >= orcAI.RallyThreshold;
    }
}