using UnityEngine;
using Unity.Netcode;

public class NonPlayableCharacter : Character
{
    
    private NetworkVariable<bool> isHostile = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Character currentTarget;

    public override void OnNetworkSpawn()
    {
       
        isHostile.OnValueChanged += OnHostileStateChanged;
    }

    public override void OnNetworkDespawn()
    {

        isHostile.OnValueChanged -= OnHostileStateChanged;
    }

  
    private void OnHostileStateChanged(bool previousValue, bool newValue)
    {
        if (newValue)
            OnBecomeHostile();
        else
            OnBecomePassive();
    }

    protected virtual void OnBecomeHostile() { }
    protected virtual void OnBecomePassive() { }

    protected override void OnDamaged(Character attacker)
    {
        if (!IsServer) return;

        base.OnDamaged(attacker);

        if (!isHostile.Value)
        {
            isHostile.Value = true;
            currentTarget = attacker;
        }
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!isHostile.Value) return;

        if (currentTarget != null)
        {
            Attack(currentTarget);
        }
    }
}