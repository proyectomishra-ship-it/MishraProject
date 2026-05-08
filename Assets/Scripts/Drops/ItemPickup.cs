using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class ItemPickup : NetworkBehaviour
{
    [Header("Item")]
    [SerializeField] private ItemData itemData;
    [SerializeField] private int quantity = 1;

    [Header("Visual")]
    [SerializeField] private float bobHeight = 0.2f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float rotationSpeed = 90f;

    private bool pickedUp;
    private Vector3 startPosition;

    public void Setup(ItemData data, int qty)
    {
        itemData = data;
        quantity = qty;
    }

    private void Awake()
    {
        // Capturar posición ANTES de que la red pueda moverlo
        startPosition = transform.position;
    }

    private void Update()
    {
        float y = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = startPosition + Vector3.up * y;
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || pickedUp) return;
        if (itemData == null) { Debug.LogWarning("[Pickup] ItemData no asignado"); return; }

        var player = other.GetComponent<Player>();
        if (player == null) return;

        if (!player.GetInventory().AddItem(itemData, quantity)) return;

        pickedUp = true;

        if (itemData is IEquippable equippable)
        {
            var equipment = player.GetEquipment();
            if (equipment != null && !equipment.IsOccupied(equippable.Slot))
            {
                bool equipped = equipment.Equip(equippable);
                Debug.Log($"[Pickup] Auto-equipado '{itemData.ItemName}' en slot {equippable.Slot}: {equipped}");
            }
        }

        Debug.Log($"[Pickup] {player.name} recogió '{itemData.ItemName}' x{quantity}");
        NetworkObject.Despawn();
    }
}