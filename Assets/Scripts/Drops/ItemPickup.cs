using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Item físico en el mundo. Se spawnea como NetworkObject por PickupSpawner.
/// Al entrar en el trigger:
///   1. El servidor transfiere el item al inventario del jugador.
///   2. Si el item es equipable y el slot correspondiente está libre, lo equipa automáticamente.
///
/// Setup del prefab:
///   - Agregar componente NetworkObject
///   - Agregar Collider con IsTrigger = true
///   - Agregar este script
///   - Registrar el prefab en la lista NetworkPrefabs del NetworkManager
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class ItemPickup : NetworkBehaviour
{
    [Header("Visual")]
    [SerializeField] private float bobHeight     = 0.2f;
    [SerializeField] private float bobSpeed      = 2f;
    [SerializeField] private float rotationSpeed = 90f;

    private ItemData itemData;
    private int      quantity;
    private bool     pickedUp;
    private Vector3  startPosition;

    /// <summary>
    /// Configurar antes de NetworkObject.Spawn().
    /// PickupSpawner lo llama automáticamente.
    /// </summary>
    public void Setup(ItemData data, int qty)
    {
        itemData = data;
        quantity = qty;
    }

    public override void OnNetworkSpawn()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        // Animación cosmética; no necesita sincronizarse.
        float y = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = startPosition + Vector3.up * y;
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || pickedUp) return;

        var player = other.GetComponent<Player>();
        if (player == null) return;

        // 1. Intentar agregar al inventario
        if (!player.GetInventory().AddItem(itemData, quantity)) return;

        pickedUp = true;

        // 2. Auto-equipar si el item es equipable y el slot está libre
        if (itemData is IEquippable equippable)
        {
            var equipment = player.GetEquipment();
            if (equipment != null && !equipment.IsOccupied(equippable.Slot))
            {
                bool equipped = equipment.Equip(equippable);
                Debug.Log($"[Pickup] Auto-equipado '{itemData.ItemName}' en slot {equippable.Slot}: {equipped}");
            }
        }

        NetworkObject.Despawn();
    }
}
