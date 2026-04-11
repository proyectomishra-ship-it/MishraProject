using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class InventoryController : NetworkBehaviour
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
        if (!IsServer) return false;

        if (itemData.Stackable)
        {
            foreach (var item in items)
            {
                if (item.Data == itemData)
                {
                    item.Add(amount);
                    NotifyInventoryChangedClientRpc(BuildInventorySnapshot(),
                        BuildOwnerRpcParams());
                    return true;
                }
            }
        }

        if (items.Count >= maxSlots)
            return false;

        items.Add(new ItemInstance(itemData, amount));
        NotifyInventoryChangedClientRpc(BuildInventorySnapshot(),
            BuildOwnerRpcParams());
        return true;
    }

    // -------------------------
    // REMOVE ITEM
    // -------------------------

    public bool RemoveItem(ItemData itemData, int amount = 1)
    {
        if (!IsServer) return false;

        foreach (var item in items)
        {
            if (item.Data == itemData)
            {
                if (item.Quantity < amount)
                    return false;

                item.Remove(amount);

                if (item.Quantity <= 0)
                    items.Remove(item);

                NotifyInventoryChangedClientRpc(BuildInventorySnapshot(),
                    BuildOwnerRpcParams());
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

    // -------------------------
    // NETWORKING
    // -------------------------

 
    private int[] BuildInventorySnapshot()
    {
        var snapshot = new int[items.Count * 2];
        for (int i = 0; i < items.Count; i++)
        {
            snapshot[i * 2] = items[i].Data is EquipmentData eq ? eq.ItemId : -1;
            snapshot[i * 2 + 1] = items[i].Quantity;
        }
        return snapshot;
    }

  
    private ClientRpcParams BuildOwnerRpcParams()
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };
    }


    [ClientRpc]
    private void NotifyInventoryChangedClientRpc(int[] snapshot, ClientRpcParams rpcParams = default)
    {
        if (IsServer) return;
        OnInventoryChanged(snapshot);
    }


    protected virtual void OnInventoryChanged(int[] snapshot) { }
}