using System.Collections.Generic;
using UnityEngine;

public class InventoryController : MonoBehaviour
{
    private Character character;

    [SerializeField] private int maxSlots = 20;

    private List<ItemInstance> items = new List<ItemInstance>();

    public void Initialize(Character character)
    {
        this.character = character;
    }

    // -------------------------
    // ADD ITEM
    // -------------------------

    public bool AddItem(ItemData itemData, int amount = 1)
    {
        // Stack si se puede
        if (itemData.Stackable)
        {
            foreach (var item in items)
            {
                if (item.Data == itemData)
                {
                    item.Add(amount);
                    return true;
                }
            }
        }

        // Nuevo slot
        if (items.Count >= maxSlots)
            return false;

        items.Add(new ItemInstance(itemData, amount));
        return true;
    }

    // -------------------------
    // REMOVE ITEM
    // -------------------------

    public bool RemoveItem(ItemData itemData, int amount = 1)
    {
        foreach (var item in items)
        {
            if (item.Data == itemData)
            {
                if (item.Quantity < amount)
                    return false;

                item.Remove(amount);

                if (item.Quantity <= 0)
                    items.Remove(item);

                return true;
            }
        }

        return false;
    }

    // -------------------------
    // GETTERS
    // -------------------------

    public List<ItemInstance> GetItems()
    {
        return items;
    }
}