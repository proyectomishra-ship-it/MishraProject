using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cofre interactuable. Al acercarse aparece "Presioná [E] para abrir";
/// al interactuar se abre y spawnea un item al azar de su tabla de loot.
/// Una vez abierto no se puede volver a abrir.
///
/// Autoridad del servidor:
///   - El cliente solo PIDE abrir (ServerRpc).
///   - El servidor valida (no abierto, jugador vivo y cerca), marca el cofre
///     como abierto y spawnea el loot.
///   - El estado "abierto" es una NetworkVariable: los clientes que entran
///     tarde ven el cofre ya abierto y sin item.
///
/// Reutiliza WeightedLootRoller + PickupSpawner, igual que DropController.
/// La tabla es un LootTableData normal (ver Loot_Chest).
///
/// ANIMACIÓN FUTURA: ver la sección "Animación" más abajo. Para incorporar
/// el arte real solo hay que asignar el Animator, no tocar código.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class Chest : NetworkBehaviour
{
    // =========================
    // LOOT
    // =========================

    [Header("Loot")]
    [Tooltip("Tabla de loot del cofre (por ejemplo Loot_Chest).")]
    [SerializeField] private LootTableData lootTable;

    [Tooltip("Donde aparece el item. Si queda vacío, 1 m sobre el cofre.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Segundos entre abrir el cofre y que aparezca el item. " +
             "Ajustarlo para que coincida con la animación de apertura.")]
    [SerializeField, Min(0f)] private float lootSpawnDelay = 0.4f;

    [Tooltip("Separación entre items si la tabla suelta más de uno.")]
    [SerializeField] private float spreadRadius = 0.6f;

    [Tooltip("Si la tirada sale vacía (dropChance < 1 o minDrops = 0), " +
             "reintenta para que el cofre nunca quede vacío.")]
    [SerializeField] private bool guaranteeAtLeastOneItem = true;

    // =========================
    // INTERACCIÓN
    // =========================

    [Header("Interacción")]
    [SerializeField] private string chestName = "cofre";
    [SerializeField] private float interactionRange = 2.5f;
    [SerializeField] private Key interactKey = Key.E;

    [Tooltip("Margen extra que acepta el servidor sobre el rango, para " +
             "absorber la latencia del jugador.")]
    [SerializeField] private float serverRangeTolerance = 1f;

    // =========================
    // ANIMACIÓN
    // =========================

    [Header("Animación (arte futuro)")]
    [Tooltip("Cuando exista el modelo del cofre: asignar su Animator acá. " +
             "Al abrirse se dispara el Trigger 'Open Trigger'.")]
    [SerializeField] private Animator animator;

    [SerializeField] private string openTrigger = "Open";

    [Tooltip("Nombre del estado final (cofre abierto). Se usa para mostrar " +
             "el cofre ya abierto, sin animación, a quien entra tarde.")]
    [SerializeField] private string openedStateName = "Opened";

    [Header("Placeholder (se ignora si hay Animator)")]
    [Tooltip("Pivote de la tapa del cofre placeholder. Rota al abrirse.")]
    [SerializeField] private Transform lidPivot;
    [SerializeField] private float lidOpenAngle = -110f;
    [SerializeField, Min(0.01f)] private float lidOpenDuration = 0.4f;

    // =========================
    // ESTADO
    // =========================

    private readonly NetworkVariable<bool> isOpened = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public bool IsOpened => isOpened.Value;

    // Cliente: evita mandar el pedido muchas veces mientras llega la respuesta.
    private float requestLockedUntil;

    private Player cachedLocalPlayer;
    private Coroutine lidRoutine;

    // =========================================================
    // NETWORK
    // =========================================================

    public override void OnNetworkSpawn()
    {
        isOpened.OnValueChanged += OnOpenedChanged;

        // Entró tarde (o se cargó la escena) con el cofre ya abierto:
        // mostrarlo abierto directamente, sin animación.
        if (isOpened.Value)
            ApplyOpenedVisual(instant: true);
    }

    public override void OnNetworkDespawn()
    {
        isOpened.OnValueChanged -= OnOpenedChanged;
        InteractionPromptUI.Hide(this);
    }

    private void OnDisable()
    {
        InteractionPromptUI.Hide(this);
    }

    private void OnOpenedChanged(bool previous, bool current)
    {
        if (current)
            ApplyOpenedVisual(instant: false);
    }

    // =========================================================
    // INTERACCIÓN (CLIENTE LOCAL)
    // =========================================================

    private void Update()
    {
        if (!IsSpawned || !IsClient)
            return;

        // Ya abierto o pedido en camino: sin aviso, sin interacción.
        if (isOpened.Value || Time.time < requestLockedUntil)
        {
            InteractionPromptUI.Hide(this);
            return;
        }

        Player player = GetLocalPlayer();

        if (player == null || player.IsDead() || !IsInRange(player, interactionRange))
        {
            InteractionPromptUI.Hide(this);
            return;
        }

        // Inventario u otra interfaz abierta: no permitir interactuar.
        var input = player.GetComponent<PlayerInputController>();

        if (input != null && input.IsInputBlocked)
        {
            InteractionPromptUI.Hide(this);
            return;
        }

        InteractionPromptUI.Show(
            this, $"{chestName} [{interactKey}]");

        if (Keyboard.current != null &&
            Keyboard.current[interactKey].wasPressedThisFrame)
        {
            // Bloqueo local breve; el servidor es quien decide de verdad.
            requestLockedUntil = Time.time + 1f;
            InteractionPromptUI.Hide(this);

            RequestOpenServerRpc();
        }
    }

    private Player GetLocalPlayer()
    {
        if (cachedLocalPlayer != null)
            return cachedLocalPlayer;

        NetworkManager nm = NetworkManager.Singleton;

        if (nm == null || !nm.IsClient || nm.LocalClient == null)
            return null;

        NetworkObject playerObject = nm.LocalClient.PlayerObject;

        if (playerObject == null)
            return null;

        cachedLocalPlayer = playerObject.GetComponent<Player>();
        return cachedLocalPlayer;
    }

    private bool IsInRange(Player player, float range)
    {
        Vector3 a = transform.position;
        Vector3 b = player.transform.position;

        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b) <= range;
    }

    // =========================================================
    // APERTURA (SERVIDOR)
    // =========================================================

    [ServerRpc(RequireOwnership = false)]
    private void RequestOpenServerRpc(ServerRpcParams rpcParams = default)
    {
        // Doble apertura (dos jugadores a la vez, spam, etc.): gana el primero.
        if (isOpened.Value)
            return;

        if (lootTable == null)
        {
            Debug.LogError($"[Chest] '{name}' no tiene LootTableData asignada.");
            return;
        }

        ulong senderId = rpcParams.Receive.SenderClientId;

        if (!NetworkManager.ConnectedClients.TryGetValue(senderId, out var client) ||
            client.PlayerObject == null)
        {
            return;
        }

        Player player = client.PlayerObject.GetComponent<Player>();

        if (player == null || player.IsDead())
            return;

        if (!IsInRange(player, interactionRange + serverRangeTolerance))
            return;

        // Se marca como abierto ANTES de esperar el delay del loot, así no
        // hay ninguna ventana en la que se pueda pedir de nuevo.
        isOpened.Value = true;

        StartCoroutine(SpawnLootAfterDelay());
    }

    private IEnumerator SpawnLootAfterDelay()
    {
        if (lootSpawnDelay > 0f)
            yield return new WaitForSeconds(lootSpawnDelay);

        var roller = new WeightedLootRoller(lootTable);

        var drops = roller.Roll();

        if (guaranteeAtLeastOneItem)
        {
            for (int attempt = 0; attempt < 10 && drops.Count == 0; attempt++)
                drops = roller.Roll();
        }

        if (drops.Count == 0)
        {
            Debug.LogWarning(
                $"[Chest] '{name}' se abrió pero la tabla no generó items. " +
                "Revisá los pesos y los Pickup Prefab de la tabla.");
            yield break;
        }

        Vector3 origin = spawnPoint != null
            ? spawnPoint.position
            : transform.position + Vector3.up;

        new PickupSpawner(spreadRadius).SpawnAll(drops, origin);

        Debug.Log($"[Chest] '{name}' abierto: {drops.Count} item(s).");
    }

    // =========================================================
    // VISUAL (TODOS LOS CLIENTES)
    // =========================================================

    private void ApplyOpenedVisual(bool instant)
    {
        // Con arte real: el Animator manda.
        if (animator != null)
        {
            if (instant)
                animator.Play(openedStateName, 0, 1f);
            else
                animator.SetTrigger(openTrigger);

            return;
        }

        // Placeholder: girar la tapa.
        if (lidPivot == null)
            return;

        if (lidRoutine != null)
            StopCoroutine(lidRoutine);

        Quaternion target = Quaternion.Euler(lidOpenAngle, 0f, 0f);

        if (instant)
            lidPivot.localRotation = target;
        else
            lidRoutine = StartCoroutine(AnimateLid(target));
    }

    private IEnumerator AnimateLid(Quaternion target)
    {
        Quaternion start = lidPivot.localRotation;
        float elapsed = 0f;

        while (elapsed < lidOpenDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / lidOpenDuration);
            lidPivot.localRotation = Quaternion.Slerp(start, target, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        lidPivot.localRotation = target;
        lidRoutine = null;
    }

    // =========================================================
    // EDITOR
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        if (spawnPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.2f);
        }
    }
}
