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
    // Agregar en OrcAIController.OnNetworkSpawn() — override del FleeState base:
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;

        // Reemplazar el FleeState genérico con el específico del Orc.
        FleeState = new OrcFleeState(GetComponent<Enemy>(), this);
    }

}