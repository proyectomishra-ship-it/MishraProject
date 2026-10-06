using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Orquestador del inventario. Une InventoryStore (lógica) con InventoryNetworkSync (red).
/// Implementa IInventory para que el resto del juego no dependa de esta clase concreta.
/// ACCIÓN: reemplaza Assets/Scripts/Controllers/InventoryController.cs
/// Mover este archivo a Assets/Scripts/Inventory/
/// </summary>
[RequireComponent(typeof(InventoryNetworkSync))]
public class InventoryController : NetworkBehaviour, IInventory
{
    [SerializeField] private int maxSlots = 20;

    private InventoryStore       store;
    private InventoryNetworkSync sync;

    public event Action OnChanged;

    private void Awake()
    {
        sync = GetComponent<InventoryNetworkSync>();
    }

    public void Initialize(Character _)
    {
        store = new InventoryStore(maxSlots);
        store.OnChanged += () => sync.Sync(store.GetAll());
        store.OnChanged += () => OnChanged?.Invoke();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            sync.Subscribe(_ => OnChanged?.Invoke());
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
            sync.Unsubscribe(_ => OnChanged?.Invoke());
    }

    // ── IInventory ───────────────────────────────────────────────────────────
    public bool AddItem(ItemData item, int amount = 1)
    {
        if (!IsServer) return false;
        return store.AddItem(item, amount);
    }

    public bool RemoveItem(ItemData item, int amount = 1)
    {
        if (!IsServer) return false;
        return store.RemoveItem(item, amount);
    }

    public bool HasItem(ItemData item, int amount = 1) => GetQuantity(item) >= amount;

    // FIX: store solo se puebla en el servidor (AddItem/RemoveItem están
    // gateados por IsServer). Un cliente puro necesita leer de la
    // NetworkList ya sincronizada en vez del store local, que para él
    // siempre está vacío. El servidor (y el host, que también es servidor)
    // siguen leyendo directo del store, sin pasar por la traducción de IDs.
    public int GetQuantity(ItemData item)
    {
        if (IsServer) return store.GetQuantity(item);

        int id = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetId(item) : -1;
        if (id < 0) return 0;

        int total = 0;
        foreach (var slot in sync.GetSlotsSnapshot())
            if (slot.ItemId == id) total += slot.Quantity;
        return total;
    }

    public IReadOnlyList<(ItemData item, int quantity)> GetAll()
    {
        if (IsServer) return store.GetAll();

        var result = new List<(ItemData item, int quantity)>();
        foreach (var slot in sync.GetSlotsSnapshot())
        {
            var item = ItemDatabase.Instance != null ? ItemDatabase.Instance.Get(slot.ItemId) : null;
            if (item != null) result.Add((item, slot.Quantity));
        }
        return result;
    }
}
