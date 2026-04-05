using System.Collections.Generic;
using UnityEngine;

public class EquipmentController : MonoBehaviour
{
    private Character character;
    private CharacterStats stats;

    private Dictionary<EquipmentSlot, EquipmentData> equippedItems
        = new Dictionary<EquipmentSlot, EquipmentData>();

    public void Initialize(Character character)
    {
        this.character = character;
        this.stats = character.GetStats();
    }

    public void Equip(EquipmentData item)
    {
        if (item == null) return;

        var slot = item.Slot;

        if (equippedItems.ContainsKey(slot))
        {
            Unequip(slot);
        }

        equippedItems[slot] = item;
        ApplyStats(item);
    }

    public void Unequip(EquipmentSlot slot)
    {
        if (!equippedItems.ContainsKey(slot))
            return;

        var item = equippedItems[slot];

        RemoveStats(item);
        equippedItems.Remove(slot);
    }

    private void ApplyStats(EquipmentData item)
    {
        foreach (var mod in item.Modifiers)
        {
            stats.AddBonus(mod.stat, mod.value);
        }
    }

    private void RemoveStats(EquipmentData item)
    {
        foreach (var mod in item.Modifiers)
        {
            stats.AddBonus(mod.stat, -mod.value);
        }
    }

    public EquipmentData GetEquipped(EquipmentSlot slot)
    {
        return equippedItems.ContainsKey(slot) ? equippedItems[slot] : null;
    }
}