using System.Collections.Generic;

/// <summary>
/// Contrato que comparten todos los items que se pueden equipar.
/// EquipmentData y WeaponData lo implementan por separado.
/// El EquipmentController trabaja contra esta interfaz, nunca contra las clases concretas.
/// </summary>
public interface IEquippable
{
    EquipmentSlot      Slot      { get; }
    List<StatModifier> Modifiers { get; }
}
