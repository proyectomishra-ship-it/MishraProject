using UnityEngine;

/// <summary>
/// Construye el comportamiento correcto
/// según el tipo de ataque del arma.
/// </summary>
public static class WeaponBehaviorFactory
{
    public static IWeaponBehavior Create(
        WeaponData weapon,
        Transform origin)
    {
        if (weapon == null)
        {
            return new MeleeWeaponBehavior(
                null,
                origin);
        }

        switch (weapon.AttackType)
        {
            case WeaponAttackType.Melee:

                return new MeleeWeaponBehavior(
                    weapon,
                    origin);

            case WeaponAttackType.Ranged:

                return new RangedWeaponBehavior(
                    weapon,
                    origin);

            case WeaponAttackType.Magic:

                return new MagicWeaponBehavior(
                    weapon,
                    origin);

            default:

                return new MeleeWeaponBehavior(
                    weapon,
                    origin);
        }
    }
}