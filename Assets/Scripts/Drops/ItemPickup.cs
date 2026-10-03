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
    private bool positionCaptured;
    private Vector3 startPosition;
    private Collider itemCollider;

    public void Setup(ItemData data, int qty)
    {
        itemData = data;
        quantity = qty;
    }

    public void SetQuantity(int qty)
    {
        quantity = qty;
    }

    private void Awake()
    {
        itemCollider = GetComponent<Collider>();
    }

    public override void OnNetworkSpawn()
    {
        // La posicion base NO se puede capturar en Awake(). Netcode instancia
        // el prefab en el cliente con su posicion por defecto (la guardada en
        // el prefab) y recien despues aplica la posicion sincronizada. En el
        // servidor si funcionaba, porque alli el objeto se instancia ya en su
        // lugar. Con Awake(), en el cliente el pickup quedaba clavado cerca del
        // origen del mundo y nunca se veia donde cayo.
        // En OnNetworkSpawn la posicion ya es la correcta en todas las maquinas.
        startPosition = transform.position;
        positionCaptured = true;
    }

    private void Update()
    {
        if (!positionCaptured || isAnimating)
            return;

        float y = Mathf.Sin(Time.time * bobSpeed) * bobHeight;

        transform.position =
            startPosition +
            Vector3.up * y;

        transform.Rotate(
            Vector3.up,
            rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || pickedUp)
            return;

        if (itemData == null)
            return;

        Player player = other.GetComponent<Player>();

        if (player == null)
            return;

        if (!player.GetInventory().AddItem(itemData, quantity))
        {
            Debug.Log($"[Pickup] {player.name} no pudo recoger {itemData.name} " +
                      "(inventario lleno o item invalido).");
            return;
        }

        Debug.Log($"[Pickup] {player.name} recogio {itemData.name} x{quantity}.");

        pickedUp = true;

        if (itemCollider != null)
            itemCollider.enabled = false;

        if (itemData is IEquippable equippable)
        {
            var equipment = player.GetEquipment();

            if (equipment != null &&
                !equipment.IsOccupied(equippable.Slot))
            {
                equipment.Equip(equippable);
            }
        }

        PlayPickupAnimationClientRpc();

        StartCoroutine(DespawnAfterAnimation());
    }

    [ClientRpc]
    private void PlayPickupAnimationClientRpc()
    {
        if (itemCollider != null)
            itemCollider.enabled = false;

        StartCoroutine(PickupAnimation());
    }

    private IEnumerator PickupAnimation()
    {
        isAnimating = true;

        Vector3 basePosition = transform.position;
        Vector3 targetPosition =
            basePosition +
            Vector3.up * pickupRiseHeight;

        Vector3 originalScale = transform.localScale;

        float elapsed = 0f;

        while (elapsed < pickupDuration)
        {
            float t = elapsed / pickupDuration;
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            transform.position =
                Vector3.Lerp(
                    basePosition,
                    targetPosition,
                    smooth);

            transform.localScale =
                Vector3.Lerp(
                    originalScale,
                    Vector3.zero,
                    smooth);

            elapsed += Time.deltaTime;

            yield return null;
        }

        transform.position = targetPosition;
        transform.localScale = Vector3.zero;
    }

    private IEnumerator DespawnAfterAnimation()
    {
        yield return new WaitForSeconds(pickupDuration);

        if (NetworkObject != null &&
            NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}