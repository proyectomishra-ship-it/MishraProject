using UnityEngine;

/// <summary>
/// Construye el IWeaponBehavior correcto para el arma equipada.
/// Al agregar un tipo de arma nuevo, solo se modifica esta clase.
/// ACCIÓN: archivo nuevo en Assets/Scripts/Weapons/
/// </summary>
public static class WeaponBehaviorFactory
{
    /// <summary>
    /// Crea el behavior para el arma dada.
    /// origin = Transform desde donde se disparan los proyectiles (generalmente el Character).
    /// </summary>
    public static IWeaponBehavior Create(WeaponData weapon, Transform origin)
    {
        if (weapon == null)
            return new MeleeWeaponBehavior(null);

        return weapon.IsRanged
            ? new RangedWeaponBehavior(weapon, origin)
            : new MeleeWeaponBehavior(weapon);
    }
}
