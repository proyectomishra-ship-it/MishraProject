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

    // -------------------------
    // EQUIP
    // -------------------------

    public void Equip(EquipmentData item)
    {
        var slot = item.Slot;

        // Si hay item equipado, desequipar primero
        if (equippedItems.ContainsKey(slot))
        {
            Unequip(slot);
        }

        equippedItems[slot] = item;

        ApplyStats(item);
    }

    // -------------------------
    // UNEQUIP
    // -------------------------

    public void Unequip(EquipmentSlot slot)
    {
        if (!equippedItems.ContainsKey(slot))
            return;

        var item = equippedItems[slot];

        RemoveStats(item);

        equippedItems.Remove(slot);
    }

    // -------------------------
    // APPLY / REMOVE STATS
    // -------------------------

    private void ApplyStats(EquipmentData item)
    {
        stats.Attack.AddBonus(item.AttackBonus);
        stats.Defense.AddBonus(item.DefenseBonus);
        stats.MaxHealth.AddBonus(item.MaxHealthBonus);
        stats.MaxMana.AddBonus(item.MaxManaBonus);
    }

    private void RemoveStats(EquipmentData item)
    {
        stats.Attack.RemoveBonus(item.AttackBonus);
        stats.Defense.RemoveBonus(item.DefenseBonus);
        stats.MaxHealth.RemoveBonus(item.MaxHealthBonus);
        stats.MaxMana.RemoveBonus(item.MaxManaBonus);
    }

    // -------------------------
    // GETTERS
    // -------------------------

    public EquipmentData GetEquipped(EquipmentSlot slot)
    {
        return equippedItems.ContainsKey(slot) ? equippedItems[slot] : null;
    }
}