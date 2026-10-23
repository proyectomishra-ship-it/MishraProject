
using UnityEngine;

/// <summary>
/// Contrato adicional para armas que admiten apuntado direccional.
/// Permite disparar sin seleccionar previamente un objetivo.
/// </summary>
public interface IAimedWeaponBehavior
{
    void PerformAimedAttack(
        Character attacker,
        Vector3 direction);

    void PerformAimedHeavyAttack(
        Character attacker,
        Vector3 direction,
        float multiplier);

    void PerformAimedSpecialAttack(
        Character attacker,
        Vector3 direction);
}
