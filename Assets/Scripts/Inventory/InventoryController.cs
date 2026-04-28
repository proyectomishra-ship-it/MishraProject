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

    public bool HasItem(ItemData item, int amount = 1) => store.HasItem(item, amount);
    public int  GetQuantity(ItemData item)             => store.GetQuantity(item);

    public IReadOnlyList<(ItemData item, int quantity)> GetAll() => store.GetAll();
}
