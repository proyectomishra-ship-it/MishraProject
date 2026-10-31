/// <summary>
/// Habilidad especial (click derecho) propia de cada clase de jugador.
/// Se asigna en PlayerClassData (Mage / Warrior / Hunter).
/// </summary>
public enum SpecialAbilityType : byte
{
    /// <summary>
    /// Sin habilidad de clase: PlayerCombatController usa el comportamiento
    /// especial del arma equipada (el flujo que existía antes).
    /// </summary>
    None = 0,

    /// <summary>Mage: onda expansiva centrada en el jugador que daña a todos los enemigos cercanos.</summary>
    MageShockwave = 1,

    /// <summary>Warrior: golpe en arco de gran alcance que daña a todos los enemigos del frente.</summary>
    WarriorCleave = 2,

    /// <summary>Hunter: ráfaga rápida de flechas con daño aumentado.</summary>
    HunterArrowBarrage = 3
}
