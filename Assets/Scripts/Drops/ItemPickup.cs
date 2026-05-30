using System.Collections;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class ItemPickup : NetworkBehaviour
{
    [Header("Item")]
    [SerializeField] private ItemData itemData;
    [SerializeField] private int quantity = 1;

    [Header("Visual — Idle")]
    [SerializeField] private float bobHeight = 0.2f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float rotationSpeed = 90f;

    [Header("Visual — Pickup")]
    [SerializeField] private float pickupRiseHeight = 1.2f;
    [SerializeField] private float pickupDuration = 0.5f;

    private bool pickedUp;
    private bool isAnimating;
    private Vector3 startPosition;
    private Collider itemCollider;

    public void Setup(ItemData data, int qty)
    {
        itemData = data;
        quantity = qty;
    }

    private void Awake()
    {
        startPosition = transform.position;
        itemCollider = GetComponent<Collider>();
    }

    private void Update()
    {
        if (isAnimating) return;

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

        // Deshabilitar collider en el servidor de inmediato —
        // ningún otro jugador puede recogerte aunque el objeto aún sea visible.
        if (itemCollider != null)
            itemCollider.enabled = false;

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

        PlayPickupAnimationClientRpc();
        StartCoroutine(DespawnAfterAnimation());
    }

    /// <summary>
    /// Se ejecuta en TODOS los clientes. Deshabilita el collider localmente
    /// y arranca la animación de recogido.
    /// </summary>
    [ClientRpc]
    private void PlayPickupAnimationClientRpc()
    {
        if (itemCollider != null)
            itemCollider.enabled = false;

        StartCoroutine(PickupAnimation());
    }

    /// <summary>
    /// El item flota hacia arriba mientras se achica hasta desaparecer.
    /// </summary>
    private IEnumerator PickupAnimation()
    {
        isAnimating = true;

        Vector3 basePos = transform.position;
        Vector3 targetPos = basePos + Vector3.up * pickupRiseHeight;
        Vector3 originalScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < pickupDuration)
        {
            float t = elapsed / pickupDuration;
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(basePos, targetPos, smooth);
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, smooth);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = Vector3.zero;
    }

    /// <summary>
    /// El servidor espera la duración de la animación y recién entonces despawnea.
    /// </summary>
    private IEnumerator DespawnAfterAnimation()
    {
        yield return new WaitForSeconds(pickupDuration);
        if (IsSpawned)
            NetworkObject.Despawn();
    }
}