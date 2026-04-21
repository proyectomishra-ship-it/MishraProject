using UnityEngine;
using Unity.Netcode;

public class GoblinAIController : EnemyAIController
{
    protected override EnemyStateAttack CreateAttackState()
    {
        return new GoblinAttackState(GetComponent<Enemy>(), this);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        base.OnNetworkSpawn();
    }

    public override void OnNetworkDespawn()
    {
        if (CurrentTarget != null)
        {
            CombatSlotManager.Instance?.RemoveFlanker(this, CurrentTarget.transform);
        }
    }
}