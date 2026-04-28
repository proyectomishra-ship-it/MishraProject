/// <summary>
/// Comportamiento de ataque para armas cuerpo a cuerpo.
/// El CombatController lo obtiene del WeaponBehaviorFactory.
/// ACCIÓN: archivo nuevo en Assets/Scripts/Weapons/
/// </summary>
public class MeleeWeaponBehavior : IWeaponBehavior
{
    private readonly WeaponData weapon;

    public MeleeWeaponBehavior(WeaponData weapon) => this.weapon = weapon;

    public void PerformAttack(Character attacker, Character target)
    {
        float damage = attacker.GetStats().Attack.Value;
        target.GetComponent<DamageReceiver>()?.TakeDamage(damage, attacker);
    }

    public void PerformHeavyAttack(Character attacker, Character target, float multiplier)
    {
        float damage = attacker.GetStats().Attack.Value * multiplier;
        target.GetComponent<DamageReceiver>()?.TakeDamage(damage, attacker);
    }

    public void PerformSpecialAttack(Character attacker, Character target)
    {
        if (weapon == null) return;
        if (!attacker.UseMana(weapon.SpecialManaCost)) return;

        float damage = attacker.GetStats().Attack.Value * weapon.SpecialMultiplier;
        target.GetComponent<DamageReceiver>()?.TakeDamage(damage, attacker);
    }
}
