using System;
using System.Collections.Generic;

/// <summary>
/// Gestiona qué item hay en cada slot de equipamiento.
/// C# puro: sin Unity, sin red, sin stats.
/// ACCIÓN: archivo nuevo en Assets/Scripts/Equipment/
/// </summary>
public class EquipmentSlotSet
{
    private readonly Dictionary<EquipmentSlot, IEquippable> slots = new();

    public event Action<EquipmentSlot, IEquippable> OnSlotChanged;

    public bool Set(IEquippable item)
    {
        if (item == null) return false;
        slots[item.Slot] = item;
        OnSlotChanged?.Invoke(item.Slot, item);
        return true;
    }

    public bool Clear(EquipmentSlot slot)
    {
        if (!slots.ContainsKey(slot)) return false;
        slots.Remove(slot);
        OnSlotChanged?.Invoke(slot, null);
        return true;
    }

    public IEquippable Get(EquipmentSlot slot) =>
        slots.TryGetValue(slot, out var v) ? v : null;

    public bool IsOccupied(EquipmentSlot slot) => slots.ContainsKey(slot);
}
