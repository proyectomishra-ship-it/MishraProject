using UnityEngine;

public class NonPlayableCharacter : Character
{
    private bool isHostile = false;

    protected override void OnDamaged(Character attacker)
    {
        base.OnDamaged(attacker);

        if (!isHostile)
        {
            isHostile = true;
            currentTarget = attacker;
        }
    }

    private void Update()
    {
        if (!isHostile) return;

        if (currentTarget != null)
        {
            Attack(currentTarget);
        }
    }
}