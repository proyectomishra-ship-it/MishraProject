using System.Collections;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class ItemPickup : NetworkBehaviour
{
    [Header("Item")]
    [SerializeField] private ItemData itemData;
    [SerializeField] private int quantity = 1;

    [Header("Recogida")]
    [Tooltip("Distancia (en metros) a la que el jugador recoge el objeto. " +
             "Se aplica al SphereCollider en Awake, asi que no depende de la " +
             "escala del prefab ni del radio guardado en el Collider.")]
    [SerializeField, Min(0.25f)] private float pickupRadius = 2.2f;

    [Tooltip("Segundos entre reintentos cuando el inventario esta lleno " +
             "(evita spam de logs y trabajo inutil).")]
    [SerializeField, Min(0.1f)] private float retryInterval = 0.5f;

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
    private float nextAttemptTime;

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
        ApplyPickupRadius();
    }

    /// <summary>
    /// Fija el radio efectivo de recogida en metros de mundo. Se divide por la
    /// escala para que un prefab escalado no cambie la distancia real.
    /// </summary>
    private void ApplyPickupRadius()
    {
        if (itemCollider is not SphereCollider sphere)
            return;

        Vector3 s = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));

        sphere.isTrigger = true;
        sphere.radius = pickupRadius / Mathf.Max(maxScale, 0.0001f);
    }

#if UNITY_EDITOR
    // Muestra el radio de recogida al seleccionar el prefab/objeto en la Scene.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
#endif

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

    private void OnTriggerEnter(Collider other) => TryPickup(other);

    // Stay: con un radio grande el jugador suele quedar DENTRO del area. Si el
    // inventario estaba lleno al entrar y luego se libera un lugar, con solo
    // OnTriggerEnter habria que salir y volver a entrar para recoger.
    private void OnTriggerStay(Collider other) => TryPickup(other);

    private void TryPickup(Collider other)
    {
        if (!IsServer || pickedUp)
            return;

        if (itemData == null)
            return;

        if (Time.time < nextAttemptTime)
            return;

        Player player = other.GetComponent<Player>();

        if (player == null)
            return;

        if (!player.GetInventory().AddItem(itemData, quantity))
        {
            nextAttemptTime = Time.time + retryInterval;

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