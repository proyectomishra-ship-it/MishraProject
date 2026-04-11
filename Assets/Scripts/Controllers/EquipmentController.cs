using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class EquipmentController : NetworkBehaviour
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
        if (!IsServer) return;
        if (item == null) return;

        var slot = item.Slot;

        if (equippedItems.ContainsKey(slot))
            Unequip(slot);

        equippedItems[slot] = item;
        ApplyStats(item);

     
        NotifyEquipClientRpc(slot, item.ItemId);
    }

    public void Unequip(EquipmentSlot slot)
    {
        if (!IsServer) return;
        if (!equippedItems.ContainsKey(slot)) return;

        var item = equippedItems[slot];
        RemoveStats(item);
        equippedItems.Remove(slot);

       
        NotifyUnequipClientRpc(slot);
    }

    private void ApplyStats(EquipmentData item)
    {
        foreach (var mod in item.Modifiers)
            stats.AddBonus(mod.stat, mod.value);
    }

    private void RemoveStats(EquipmentData item)
    {
        foreach (var mod in item.Modifiers)
            stats.AddBonus(mod.stat, -mod.value);
    }

    public EquipmentData GetEquipped(EquipmentSlot slot)
    {
        return equippedItems.ContainsKey(slot) ? equippedItems[slot] : null;
    }


    [ClientRpc]
    private void NotifyEquipClientRpc(EquipmentSlot slot, int itemId)
    {
        
        if (IsServer) return;
        OnItemEquipped(slot, itemId);
    }

    [ClientRpc]
    private void NotifyUnequipClientRpc(EquipmentSlot slot)
    {
        if (IsServer) return;
        OnItemUnequipped(slot);
    }

  
    protected virtual void OnItemEquipped(EquipmentSlot slot, int itemId) { }
    protected virtual void OnItemUnequipped(EquipmentSlot slot) { }
}