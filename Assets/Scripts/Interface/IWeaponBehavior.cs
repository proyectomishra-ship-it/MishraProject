/// <summary>
/// Contrato de comportamiento de un arma.
/// Cada tipo de arma tiene su propia implementación.
/// El CombatController puede obtener el behavior actual del arma equipada
/// y delegar sin saber qué tipo de arma es.
/// </summary>
public interface IWeaponBehavior
{
    void PerformAttack(Character attacker, Character target);
    void PerformHeavyAttack(Character attacker, Character target, float multiplier);
    void PerformSpecialAttack(Character attacker, Character target);
}
