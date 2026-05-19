using UnityEngine;
using Unity.Netcode;

public class GoblinAIController : EnemyAIController
{
    protected override EnemyStateAttack CreateAttackState()
    {
        return new GoblinAttackState(
            GetComponent<Enemy>(),
            this);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (CurrentTarget != null)
        {
            CombatSlotManager.Instance
                ?.RemoveFlanker(
                    this,
                    CurrentTarget.transform);
        }
    }
}