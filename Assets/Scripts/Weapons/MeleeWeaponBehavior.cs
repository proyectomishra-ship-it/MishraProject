using UnityEngine;

public class MeleeWeaponBehavior : IWeaponBehavior
{
    private readonly WeaponData weapon;

    private readonly Transform origin;

    private const float sphereRadius = 1f;

    public MeleeWeaponBehavior(
        WeaponData weapon,
        Transform origin)
    {
        this.weapon = weapon;
        this.origin = origin;
    }

    public void ExecuteAttack(
        Character attacker,
        Character target,
        bool heavy)
    {
        if (target == null)
            return;

        if (!ValidateHit(target))
            return;

        float damage =
            attacker.GetStats().Attack.Value;

        if (weapon != null && heavy)
        {
            damage *= weapon.HeavyMultiplier;
        }

        AttackData attackData =
            new AttackData
            {
                Attacker = attacker,
                Target = target,
                Damage = damage,
                DamageType =
                    weapon != null
                        ? weapon.DamageType
                        : DamageType.Physical,
                IsHeavy = heavy,
                IsCritical = false,
                HitPoint = target.transform.position
            };

        DamageReceiver receiver =
            target.GetComponent<DamageReceiver>();

        if (receiver == null)
        {
            Debug.LogError(
                "[MeleeWeaponBehavior] Missing DamageReceiver");

            return;
        }

        receiver.TakeDamage(attackData);
    }

    public void ExecuteSpecialAttack(
        Character attacker,
        Character target)
    {
        ExecuteAttack(
            attacker,
            target,
            true);
    }

    private bool ValidateHit(Character target)
    {
        Vector3 originPos =
            origin.position + Vector3.up;

        Vector3 targetPos =
            target.transform.position + Vector3.up;

        Vector3 direction =
            (targetPos - originPos).normalized;

        float distance =
            Vector3.Distance(
                originPos,
                targetPos);

        return Physics.SphereCast(
            originPos,
            sphereRadius,
            direction,
            out RaycastHit hit,
            distance + 0.5f);
    }
}