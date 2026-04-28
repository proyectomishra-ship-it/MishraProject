using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// Traduce el InventoryStore (servidor) a una NetworkList visible solo por el dueño.
/// No tiene lógica de negocio. Solo sincroniza.
/// ACCIÓN: archivo nuevo en Assets/Scripts/Inventory/
/// InventoryController lo agrega automáticamente via [RequireComponent].
/// </summary>
public class InventoryNetworkSync : NetworkBehaviour
{
    public struct Slot : INetworkSerializable, System.IEquatable<Slot>
    {
        public int ItemId;
        public int Quantity;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref ItemId);
            s.SerializeValue(ref Quantity);
        }

        public bool Equals(Slot other) =>
            ItemId == other.ItemId && Quantity == other.Quantity;
    }

    private NetworkList<Slot> networkSlots;

    private void Awake()
    {
        networkSlots = new NetworkList<Slot>(
            null,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server
        );
    }

    /// <summary>
    /// Reconstruye la NetworkList desde el snapshot actual del Store.
    /// Solo llamar en servidor cuando el Store cambia.
    /// </summary>
    public void Sync(IReadOnlyList<(ItemData item, int quantity)> snapshot)
    {
        if (!IsServer) return;

        networkSlots.Clear();

        foreach (var (item, qty) in snapshot)
        {
            int id = ItemDatabase.Instance.GetId(item);
            if (id >= 0)
                networkSlots.Add(new Slot { ItemId = id, Quantity = qty });
        }
    }

    public void Subscribe(NetworkList<Slot>.OnListChangedDelegate cb)
        => networkSlots.OnListChanged += cb;

    public void Unsubscribe(NetworkList<Slot>.OnListChangedDelegate cb)
        => networkSlots.OnListChanged -= cb;
}
