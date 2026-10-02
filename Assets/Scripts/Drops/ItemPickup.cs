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

    public void SetQuantity(int qty)
    {
        quantity = qty;
    }

    private void Awake()
    {
        startPosition = transform.position;
        itemCollider = GetComponent<Collider>();
    }

    private void Update()
    {
        if (isAnimating)
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
            return;

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