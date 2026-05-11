using UnityEngine;

public class OrcAIController : EnemyAIController
{
    [Header("Retreat and Rally")]

    [SerializeField] private float retreatThreshold = 0.3f;

    [SerializeField] private float rallyThreshold = 0.7f;

    [SerializeField] private float regenPerSecond = 5f;

    public float RetreatThreshold => retreatThreshold;
    public float RallyThreshold => rallyThreshold;
    public float RegenPerSecond => regenPerSecond;

    protected override EnemyStateAttack CreateAttackState()
    {
        return new OrcAttackState(GetComponent<Enemy>(), this);
    }

    public override void OnNetworkSpawn()
    {
        
        if (!IsServer) return;

        base.OnNetworkSpawn();

        FleeState = new OrcFleeState(GetComponent<Enemy>(), this);

        Debug.Log($"[OrcAI] {name} initialized");
    }
}