using System;
using System.Collections.Generic;

/// <summary>
/// Logica pura del inventario. Sin Unity, sin red. 100% testeable.
/// El servidor la usa como fuente de verdad.
/// 
/// FIX: el stacking ahora maneja overflow correctamente.
/// Si un stack se llena, el excedente se distribuye en nuevos slots.
/// </summary>
/// <summary>
/// Opcional: permite agrupar varias operaciones del inventario y emitir un
/// único OnChanged al final (un craft = un solo Sync de red en vez de uno
/// por ingrediente). Se mantiene separada de IInventory para no romper
/// otras implementaciones.
/// </summary>
public interface IBatchable
{
    void BeginBatch();
    void EndBatch();
}

public class InventoryStore : IInventory, IBatchable
{
    private readonly List<(ItemData item, int quantity)> slots = new();
    private readonly int maxSlots;

    public event Action OnChanged;

    private int batchDepth;
    private bool dirtyInBatch;

    public InventoryStore(int maxSlots) => this.maxSlots = maxSlots;

    public void BeginBatch() => batchDepth++;

    public void EndBatch()
    {
        if (batchDepth == 0) return;
        batchDepth--;
        if (batchDepth == 0 && dirtyInBatch)
        {
            dirtyInBatch = false;
            OnChanged?.Invoke();
        }
    }

    private void NotifyChanged()
    {
        if (batchDepth > 0) { dirtyInBatch = true; return; }
        OnChanged?.Invoke();
    }

    public bool AddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;

        if (item.Stackable)
            return AddStackable(item, amount);

        // No stackable: cada unidad ocupa un slot
        if (slots.Count >= maxSlots) return false;
        slots.Add((item, amount));
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// FIX: antes solo miraba el primer slot que contuviera el item y
    /// fallaba si ESE slot no alcanzaba, aun cuando el total en el
    /// inventario sí era suficiente (típico con materiales de crafteo
    /// repartidos en varios stacks tras varios pickups). Ahora suma across
    /// slots, igual que ya hacían GetQuantity/HasItem. Todo o nada: si el
    /// total no alcanza, no se toca ningún slot.
    /// </summary>
    public bool RemoveItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;
        if (GetQuantity(item) < amount) return false;

        int remaining = amount;

        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i].item != item) continue;

            int toRemove = Math.Min(slots[i].quantity, remaining);
            remaining -= toRemove;

            if (slots[i].quantity == toRemove)
            {
                slots.RemoveAt(i);
                i--; // compensar el corrimiento de índices tras el RemoveAt
            }
            else
            {
                slots[i] = (item, slots[i].quantity - toRemove);
            }
        }

        NotifyChanged();
        return true;
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
            NotifyChanged();
            return true;
        }

        return false;
    }
}