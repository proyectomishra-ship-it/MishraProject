using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Orquestador del equipamiento. Une EquipmentSlotSet, EquipmentStatApplicator
/// y EquipmentNetworkSync. Trabaja contra IEquippable, nunca contra clases concretas.
/// ACCIÓN: reemplaza Assets/Scripts/Controllers/EquipmentController.cs
/// Mover este archivo a Assets/Scripts/Equipment/
/// </summary>
[RequireComponent(typeof(EquipmentNetworkSync))]
public class EquipmentController : NetworkBehaviour
{
    private EquipmentSlotSet        slots;
    private EquipmentStatApplicator applicator;
    private EquipmentNetworkSync    sync;

    /// <summary>
    /// Slot modificado + item nuevo (null = desequipado).
    /// Se dispara en todos los clientes.
    /// </summary>
    public event Action<EquipmentSlot, IEquippable> OnSlotChanged;

    private void Awake() => sync = GetComponent<EquipmentNetworkSync>();

    public void Initialize(Character owner)
    {
        slots      = new EquipmentSlotSet();
        applicator = new EquipmentStatApplicator(owner.GetStats());
        slots.OnSlotChanged += HandleLocalSlotChanged;
    }

    public override void OnNetworkSpawn()
        => sync.Subscribe(HandleNetworkChanged);

    public override void OnNetworkDespawn()
        => sync.Unsubscribe(HandleNetworkChanged);

    // ── API pública ──────────────────────────────────────────────────────────

    /// <summary>
    /// Equipa un item. Desequipa el slot previo si estaba ocupado.
    /// No toca el inventario — eso es responsabilidad de quien llama.
    /// Solo servidor.
    /// </summary>
    public bool Equip(IEquippable item)
    {
        if (!IsServer) return false;
        if (item == null) return false;

        int id = ItemDatabase.Instance.GetId(item as ItemData);
        if (id < 0)
        {
            Debug.LogWarning("[Equipment] Item no está en ItemDatabase.");
            return false;
        }

        if (slots.IsOccupied(item.Slot))
            UnequipInternal(item.Slot);

        applicator.Apply(item);
        return slots.Set(item);
    }

    /// <summary>Desequipa el slot indicado. Solo servidor.</summary>
    public bool Unequip(EquipmentSlot slot)
    {
        if (!IsServer) return false;
        return UnequipInternal(slot);
    }

    /// <summary>
    /// Devuelve el item equipado en ese slot.
    /// Funciona en todos los clientes (lee la NetworkVariable).
    /// </summary>
    public IEquippable GetEquipped(EquipmentSlot slot)
    {
        int id = sync.GetSlotId(slot);
        return id >= 0 ? ItemDatabase.Instance?.Get(id) as IEquippable : null;
    }

    /// <summary>Conveniencia: devuelve el WeaponData equipado, o null.</summary>
    public WeaponData GetEquippedWeapon() =>
        GetEquipped(EquipmentSlot.Weapon) as WeaponData;

    public bool IsOccupied(EquipmentSlot slot) => sync.GetSlotId(slot) >= 0;

    // ── Privado ──────────────────────────────────────────────────────────────

    private bool UnequipInternal(EquipmentSlot slot)
    {
        var item = slots.Get(slot);
        if (item == null) return false;
        applicator.Revert(item);
        return slots.Clear(slot);
    }

    private void HandleLocalSlotChanged(EquipmentSlot slot, IEquippable item)
    {
        int id = item != null ? ItemDatabase.Instance.GetId(item as ItemData) : -1;
        sync.UpdateSlot(slot, id);
    }

    private void HandleNetworkChanged(
        EquipmentNetworkSync.Snapshot _,
        EquipmentNetworkSync.Snapshot __)
    {
        foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            OnSlotChanged?.Invoke(slot, GetEquipped(slot));
    }
}
