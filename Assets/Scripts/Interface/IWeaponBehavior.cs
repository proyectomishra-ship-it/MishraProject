/// <summary>
/// Contrato universal de comportamiento de armas.
/// </summary>
public interface IWeaponBehavior
{
    void ExecuteAttack(
        Character attacker,
        Character target,
        bool heavy);

    void ExecuteSpecialAttack(
        Character attacker,
        Character target);
}