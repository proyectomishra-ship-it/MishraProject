using System;
using System.Collections.Generic;

/// <summary>
/// Contrato del inventario. Permite cambiar la implementación
/// sin tocar ningún sistema que lo consuma.
/// </summary>
public interface IInventory
{
    bool AddItem(ItemData item, int amount = 1);
    bool RemoveItem(ItemData item, int amount = 1);
    bool HasItem(ItemData item, int amount = 1);
    int  GetQuantity(ItemData item);

    IReadOnlyList<(ItemData item, int quantity)> GetAll();

    event Action OnChanged;
}
