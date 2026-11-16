using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cofre híbrido.
///
///   - En el EDITOR de Unity: cofre reutilizable. Se abre todas las veces que
///     se quiera y suelta "itemsPerOpen" items (4 por defecto) en cada apertura.
///   - En BUILDS (incluidas las Development): cofre común. Se abre una sola
///     vez, suelta un item al azar de su tabla y queda abierto para siempre.
///
/// Autoridad del servidor:
///   - El cliente solo PIDE abrir (Rpc).
///   - El servidor valida, decide el modo y spawnea el loot.
///   - Quien manda es el modo del SERVIDOR/HOST. Si el host es el Editor, el
///     cofre es reutilizable para todos, aunque un cliente sea una build.
///
/// IMPORTANTE: no hay bloques #if alrededor de campos, NetworkVariables ni
/// RPCs. Netcode necesita que Editor y build tengan exactamente la misma
/// estructura de red, si no se rompe la conexión entre ellos. La diferencia
/// de comportamiento se resuelve en runtime con Application.isEditor.
///
/// Reutiliza WeightedLootRoller + PickupSpawner.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class HybridChest : NetworkBehaviour
{
    /// <summary>
    /// true solo dentro del Editor de Unity (Play Mode).
    /// En cualquier build es false.
    /// </summary>
    private static bool ReusableMode => Application.isEditor;

    // =========================
    // LOOT
    // =========================

    [Header("Loot")]
    [Tooltip("Tabla de loot del cofre (por ejemplo Loot_Chest).")]
    [SerializeField] private LootTableData lootTable;

    [Tooltip("Donde aparece el item. Si queda vacío, 1 m sobre el cofre.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Segundos entre abrir el cofre y que aparezca el item.")]
    [SerializeField, Min(0f)] private float lootSpawnDelay = 0.4f;

    [Tooltip("Separación entre items si se suelta más de uno.")]
    [SerializeField] private float spreadRadius = 0.9f;

    [Tooltip("Build: reintenta la tirada para evitar que el cofre quede vacío.")]
    [SerializeField] private bool guaranteeAtLeastOneItem = true;

    // =========================
    // MODO EDITOR (REUTILIZABLE)
    // =========================

    [Header("Modo Editor (reutilizable)")]
    [Tooltip("Solo Editor: cantidad de items por apertura.")]
    [SerializeField, Min(1)] private int itemsPerOpen = 4;

    [Tooltip("Solo Editor: máximo de tiradas por apertura para juntar los " +
             "items pedidos.")]
    [SerializeField, Min(1)] private int maxRollAttempts = 20;

    [Tooltip("Solo Editor: segundos que queda abierto tras soltar el loot.")]
    [SerializeField, Min(0f)] private float openHoldTime = 0.6f;

    [Tooltip("Solo Editor: espera extra tras cerrarse antes de poder reabrir.")]
    [SerializeField, Min(0f)] private float reopenCooldown = 0.5f;

    // =========================
    // INTERACCIÓN
    // =========================

    [Header("Interacción")]
    [SerializeField] private string chestName = "cofre";
    [SerializeField] private float interactionRange = 2.5f;
    [SerializeField] private Key interactKey = Key.E;

    [Tooltip("Margen extra aceptado por el servidor para absorber latencia.")]
    [SerializeField] private float serverRangeTolerance = 1f;

    // =========================
    // ANIMACIÓN
    // =========================

    [Header("Animación (arte futuro)")]
    [SerializeField] private Animator animator;

    [SerializeField] private string openTrigger = "Open";

    [Tooltip("Solo Editor: trigger para volver al estado cerrado. " +
             "Dejalo vacío si tu Animator vuelve solo al idle.")]
    [SerializeField] private string closeTrigger = "Close";

    [Tooltip("Build: estado final del cofre abierto.")]
    [SerializeField] private string openedStateName = "Opened";

    [Header("Placeholder (se ignora si hay Animator)")]
    [SerializeField] private Transform lidPivot;
    [SerializeField] private float lidOpenAngle = -110f;
    [SerializeField, Min(0.01f)] private float lidOpenDuration = 0.4f;

    // =========================
    // ESTADO
    // =========================

    // Modo build: abierto para siempre.
    private readonly NetworkVariable<bool> isOpened =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    // Modo Editor: ocupado mientras dura la secuencia abrir/cerrar.
    private readonly NetworkVariable<bool> isBusy =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public bool IsOpened => isOpened.Value;
    public bool IsBusy => isBusy.Value;

    private float requestLockedUntil;

    private Player cachedLocalPlayer;
    private Coroutine lidRoutine;

    // =========================================================
    // NETWORK
    // =========================================================

    public override void OnNetworkSpawn()
    {
        isOpened.OnValueChanged += OnOpenedChanged;

        // Jugador que entra tarde: mostrar el cofre abierto directamente.
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

        // Ya abierto (build), ocupado (Editor) o solicitud en camino.
        // Se chequean ambos estados para que un cliente funcione bien
        // sin importar el modo del servidor.
        if (isOpened.Value ||
            isBusy.Value ||
            Time.time < requestLockedUntil)
        {
            InteractionPromptUI.Hide(this);
            return;
        }

        Player player = GetLocalPlayer();

        if (player == null ||
            player.IsDead() ||
            !IsInRange(player, interactionRange))
        {
            InteractionPromptUI.Hide(this);
            return;
        }

        // Bloquear interacción si hay otra interfaz abierta.
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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestOpenServerRpc(RpcParams rpcParams = default)
    {
        // Evitar que dos jugadores abran el mismo cofre a la vez,
        // o que se abra uno ya abierto (modo build).
        if (isOpened.Value || isBusy.Value)
            return;

        if (lootTable == null)
        {
            Debug.LogError(
                $"[HybridChest] '{name}' no tiene LootTableData asignada.");
            return;
        }

        // Identificar al cliente que envió la solicitud.
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (!NetworkManager.ConnectedClients.TryGetValue(
                senderId, out var client) ||
            client.PlayerObject == null)
        {
            return;
        }

        Player player = client.PlayerObject.GetComponent<Player>();

        if (player == null || player.IsDead())
            return;

        // Validación de distancia en el servidor.
        if (!IsInRange(player, interactionRange + serverRangeTolerance))
            return;

        if (ReusableMode)
        {
            // Editor: secuencia reutilizable.
            isBusy.Value = true;

            StartCoroutine(ReusableOpenSequence());
        }
        else
        {
            // Build: comportamiento del cofre común.
            isOpened.Value = true;

            StartCoroutine(SpawnLootAfterDelay());
        }
    }

    // ---------------------------------------------------------
    // MODO BUILD (cofre común)
    // ---------------------------------------------------------

    private IEnumerator SpawnLootAfterDelay()
    {
        if (lootSpawnDelay > 0f)
            yield return new WaitForSeconds(lootSpawnDelay);

        var roller = new WeightedLootRoller(lootTable);

        var drops = roller.Roll();

        // Garantizar al menos un item si está habilitado.
        if (guaranteeAtLeastOneItem)
        {
            for (int attempt = 0;
                 attempt < 10 && drops.Count == 0;
                 attempt++)
            {
                drops = roller.Roll();
            }
        }

        if (drops.Count == 0)
        {
            Debug.LogWarning(
                $"[HybridChest] '{name}' se abrió pero la tabla no generó " +
                "items. Revisá los pesos y los Pickup Prefab de la tabla.");

            yield break;
        }

        new PickupSpawner(spreadRadius).SpawnAll(drops, GetSpawnOrigin());

        Debug.Log(
            $"[HybridChest] '{name}' abierto: {drops.Count} item(s).");
    }

    // ---------------------------------------------------------
    // MODO EDITOR (reutilizable, N items)
    // ---------------------------------------------------------

    private IEnumerator ReusableOpenSequence()
    {
        PlayOpenVisualRpc();

        if (lootSpawnDelay > 0f)
            yield return new WaitForSeconds(lootSpawnDelay);

        SpawnMultipleItems();

        if (openHoldTime > 0f)
            yield return new WaitForSeconds(openHoldTime);

        PlayCloseVisualRpc();

        // Esperar a que termine de cerrarse + cooldown.
        float wait = lidOpenDuration + reopenCooldown;

        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        // Listo para volver a usarse.
        isBusy.Value = false;
    }

    private void SpawnMultipleItems()
    {
        var roller = new WeightedLootRoller(lootTable);

        // Se juntan tiradas hasta llegar a itemsPerOpen.
        // (Asume que Roll() devuelve una List<T>.)
        var drops = roller.Roll();

        for (int attempt = 1;
             attempt < maxRollAttempts && drops.Count < itemsPerOpen;
             attempt++)
        {
            drops.AddRange(roller.Roll());
        }

        // Si una tirada soltó de más, recortar al número exacto.
        if (drops.Count > itemsPerOpen)
            drops.RemoveRange(itemsPerOpen, drops.Count - itemsPerOpen);

        if (drops.Count == 0)
        {
            Debug.LogWarning(
                $"[HybridChest] '{name}' se abrió pero la tabla no generó " +
                "items. Revisá los pesos y los Pickup Prefab de la tabla.");
            return;
        }

        if (drops.Count < itemsPerOpen)
        {
            Debug.LogWarning(
                $"[HybridChest] '{name}' solo pudo generar {drops.Count} de " +
                $"{itemsPerOpen} items tras {maxRollAttempts} tiradas.");
        }

        new PickupSpawner(spreadRadius).SpawnAll(drops, GetSpawnOrigin());

        Debug.Log(
            $"[HybridChest] '{name}' (Editor) abierto: " +
            $"{drops.Count} item(s).");
    }

    // ---------------------------------------------------------
    // COMÚN
    // ---------------------------------------------------------

    private Vector3 GetSpawnOrigin()
    {
        return spawnPoint != null
            ? spawnPoint.position
            : transform.position + Vector3.up;
    }

    // =========================================================
    // VISUAL (TODOS LOS CLIENTES)
    // =========================================================

    // Modo build: el cofre queda abierto (lo dispara isOpened).
    private void ApplyOpenedVisual(bool instant)
    {
        // Si existe Animator, utilizar la animación del modelo.
        if (animator != null)
        {
            if (instant)
                animator.Play(openedStateName, 0, 1f);
            else
                animator.SetTrigger(openTrigger);

            return;
        }

        // Placeholder: rotar la tapa del cofre.
        if (lidPivot == null)
            return;

        MoveLid(Quaternion.Euler(lidOpenAngle, 0f, 0f), instant);
    }

    // Modo Editor: abrir y cerrar cada vez.
    [Rpc(SendTo.Everyone)]
    private void PlayOpenVisualRpc()
    {
        if (animator != null)
        {
            if (!string.IsNullOrEmpty(closeTrigger))
                animator.ResetTrigger(closeTrigger);

            animator.SetTrigger(openTrigger);
            return;
        }

        MoveLid(Quaternion.Euler(lidOpenAngle, 0f, 0f), instant: false);
    }

    [Rpc(SendTo.Everyone)]
    private void PlayCloseVisualRpc()
    {
        if (animator != null)
        {
            if (!string.IsNullOrEmpty(closeTrigger))
            {
                animator.ResetTrigger(openTrigger);
                animator.SetTrigger(closeTrigger);
            }

            return;
        }

        MoveLid(Quaternion.identity, instant: false);
    }

    private void MoveLid(Quaternion target, bool instant)
    {
        if (lidPivot == null)
            return;

        if (lidRoutine != null)
            StopCoroutine(lidRoutine);

        if (instant)
        {
            lidPivot.localRotation = target;
            lidRoutine = null;
        }
        else
        {
            lidRoutine = StartCoroutine(AnimateLid(target));
        }
    }

    private IEnumerator AnimateLid(Quaternion target)
    {
        Quaternion start = lidPivot.localRotation;

        float elapsed = 0f;

        while (elapsed < lidOpenDuration)
        {
            float t = Mathf.SmoothStep(
                0f, 1f, elapsed / lidOpenDuration);

            lidPivot.localRotation = Quaternion.Slerp(
                start, target, t);

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

        Gizmos.DrawWireSphere(
            transform.position, interactionRange);

        if (spawnPoint != null)
        {
            Gizmos.color = Color.yellow;

            Gizmos.DrawWireSphere(
                spawnPoint.position, 0.2f);
        }
    }
}
