using System;
using System.Collections.Generic;

/// <summary>
/// Logica pura del inventario. Sin Unity, sin red. 100% testeable.
/// El servidor la usa como fuente de verdad.
/// 
/// FIX: el stacking ahora maneja overflow correctamente.
/// Si un stack se llena, el excedente se distribuye en nuevos slots.
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

        if (item.Stackable)
            return AddStackable(item, amount);

        // No stackable: cada unidad ocupa un slot
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

    // =========================
    // STACKING CON OVERFLOW
    // =========================

    /// <summary>
    /// Agrega items stackables distribuyendo el excedente en nuevos slots si es necesario.
    /// Devuelve true si se pudo agregar TODO el amount. False si no habia espacio suficiente.
    /// </summary>
    private bool AddStackable(ItemData item, int amount)
    {
        int remaining = amount;

        // Paso 1: intentar llenar stacks existentes
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i].item != item) continue;

            int space = item.MaxStack - slots[i].quantity;
            if (space <= 0) continue;

            int toAdd = Math.Min(space, remaining);
            slots[i] = (item, slots[i].quantity + toAdd);
            remaining -= toAdd;
        }

        // Paso 2: crear nuevos slots para el excedente
        while (remaining > 0)
        {
            if (slots.Count >= maxSlots) return false; // Sin espacio

            int toAdd = Math.Min(item.MaxStack, remaining);
            slots.Add((item, toAdd));
            remaining -= toAdd;
        }

        if (remaining == 0)
        {
            OnChanged?.Invoke();
            return true;
        }

        return false;
    }
}