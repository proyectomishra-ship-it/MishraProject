using System;
using System.Collections.Generic;

/// <summary>
/// Lógica pura del inventario. Sin Unity, sin red. 100% testeable.
/// El servidor la usa como fuente de verdad.
/// ACCIÓN: archivo nuevo en Assets/Scripts/Inventory/
/// </summary>
public class InventoryStore : IInventory
{
    private readonly List<(ItemData item, int quantity)> slots = new();
    private readonly int maxSlots;

    public event Action OnChanged;

    public InventoryStore(int maxSlots) => this.maxSlots = maxSlots;

    public bool AddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;
        if (item.Stackable && TryStack(item, amount)) return true;
        if (slots.Count >= maxSlots) return false;

        slots.Add((item, amount));
        OnChanged?.Invoke();
        return true;
    }

    public bool RemoveItem(ItemData item, int amount = 1)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].item != item) continue;
            if (slots[i].quantity < amount) return false;

            if (slots[i].quantity == amount)
                slots.RemoveAt(i);
            else
                slots[i] = (item, slots[i].quantity - amount);

            OnChanged?.Invoke();
            return true;
        }
        return false;
    }

    public bool HasItem(ItemData item, int amount = 1) => GetQuantity(item) >= amount;

    public int GetQuantity(ItemData item)
    {
        int total = 0;
        foreach (var s in slots)
            if (s.item == item) total += s.quantity;
        return total;
    }

    public IReadOnlyList<(ItemData item, int quantity)> GetAll() => slots;

    private bool TryStack(ItemData item, int amount)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].item != item) continue;
            int next = slots[i].quantity + amount;
            if (next > item.MaxStack) continue;
            slots[i] = (item, next);
            OnChanged?.Invoke();
            return true;
        }
        return false;
    }
}
