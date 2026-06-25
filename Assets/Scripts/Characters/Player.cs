using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class Player : Character
{
    [SerializeField] private PlayerClassData classData;

    private PlayerHUD hud;
    private CharacterStatsSyncController statsSync;

    private PlayerInputController inputController;
    private MovementController movementController;

    private PlayerCombatController playerCombatController;

    private InventoryUI inventoryUI;

    protected override void Awake()
    {
        base.Awake();

        inputController = GetComponent<PlayerInputController>();
        movementController = GetComponent<MovementController>();
        playerCombatController = GetComponent<PlayerCombatController>();

        if (inputController == null)
            Debug.LogError("[Player] Falta PlayerInputController");

        if (movementController == null)
            Debug.LogError("[Player] Falta MovementController");

        if (playerCombatController == null)
            Debug.LogError("[Player] Falta PlayerCombatController");

        movementController?.Initialize(this);
        playerCombatController?.Initialize(this);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            // Jugador remoto: ocultar todos los renderers (Skinned, Mesh, etc.)
            // para que no aparezcan en la cámara del jugador local.
            // Cada cliente ve solo su propio personaje.
            OcultarRenderersRemotos();
            return;
        }

        inputController?.Initialize(this);

        statsSync = GetComponent<CharacterStatsSyncController>();

        if (statsSync == null)
        {
            Debug.LogError("[Player] Falta CharacterStatsSyncController");
            return;
        }

        inventoryUI = FindFirstObjectByType<InventoryUI>();

        if (inventoryUI == null)
            Debug.LogWarning("[Player] No se encontro InventoryUI");

        // Inicializar HUD e inventario en coroutine para evitar
        // problemas de timing al cargar la escena
        StartCoroutine(InitializeWhenReady());
    }

    private IEnumerator InitializeWhenReady()
    {
        // Esperar que los stats estén listos
        yield return new WaitUntil(
            () => statsSync != null && statsSync.NetMaxHealth.Value > 0);

        // Buscar HUD con reintentos por si la escena aún está cargando
        float timeout = 5f;
        float elapsed = 0f;
        while (hud == null && elapsed < timeout)
        {
            hud = FindFirstObjectByType<PlayerHUD>();
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (hud == null)
        {
            Debug.LogError("[Player] PlayerHUD no encontrado después de esperar.");
        }
        else
        {
            hud.Initialize(statsSync);
        }

        // Esperar un frame extra para que el transform esté en posición final
        yield return null;

        // Inicializar el inventario después para que la cámara de preview
        // reciba el transform ya posicionado correctamente
        if (inventoryUI != null)
        {
            inventoryUI.Initialize(
                inventoryController,
                equipmentController,
                this);
        }
    }

    protected override CharacterStats CreateStats()
    {
        return new PlayerStats(characterData, classData);
    }

    public void AddExp(int amount)
    {
        if (!IsServer) return;

        ((PlayerStats)stats).AddExperience(amount);
    }

    // =====================================================
    // INVENTORY / EQUIPMENT API
    // Llamados desde InventoryUI en el cliente local.
    // =====================================================

    public void RequestEquip(int itemId)
    {
        if (IsOwner) EquipServerRpc(itemId);
    }

    public void RequestUnequip(EquipmentSlot slot)
    {
        if (IsOwner) UnequipServerRpc((int)slot);
    }

    [ServerRpc]
    private void EquipServerRpc(int itemId)
    {
        var item = ItemDatabase.Instance.Get(itemId);
        if (item is not IEquippable equippable) return;
        bool ok = equipmentController.Equip(equippable);
        Debug.Log($"[Player] Equipado '{item.ItemName}': {ok}");
    }

    [ServerRpc]
    private void UnequipServerRpc(int slotIndex)
    {
        bool ok = equipmentController.Unequip((EquipmentSlot)slotIndex);
        Debug.Log($"[Player] Desequipado slot {(EquipmentSlot)slotIndex}: {ok}");
    }

    // =====================================================
    // MOVEMENT
    // =====================================================

    public void Move(
        Vector3 worldDirection,
        Quaternion rotation)
    {
        if (IsOwner)
            MoveServerRpc(worldDirection, rotation);
    }

    public void Run(
        Vector3 worldDirection,
        Quaternion rotation)
    {
        if (IsOwner)
            RunServerRpc(worldDirection, rotation);
    }

    public void Stop()
    {
        if (IsOwner)
            StopServerRpc();
    }

    /// <summary>
    /// Bloquea o desbloquea el input del jugador (movimiento y ataque).
    /// Llamar al abrir/cerrar el inventario.
    /// </summary>
    public void SetInputBlocked(bool blocked)
    {
        if (inputController != null)
            inputController.IsInputBlocked = blocked;
    }

    public void Jump()
    {
        if (IsOwner)
            JumpServerRpc();
    }

    public void ApplyGravity()
    {
        if (IsOwner)
            ApplyGravityServerRpc();
    }

    [ServerRpc]
    private void MoveServerRpc(
        Vector3 worldDirection,
        Quaternion rotation)
    {
        movementController?.Move(
            worldDirection,
            rotation);
    }

    [ServerRpc]
    private void RunServerRpc(
        Vector3 worldDirection,
        Quaternion rotation)
    {
        movementController?.Run(
            worldDirection,
            rotation);
    }

    [ServerRpc]
    private void StopServerRpc()
    {
        movementController?.Stop();
    }

    [ServerRpc]
    private void JumpServerRpc()
    {
        movementController?.Jump();
    }

    [ServerRpc]
    private void ApplyGravityServerRpc()
    {
        movementController?.ApplyGravity();
    }

    // =====================================================
    // COMBAT
    // =====================================================

    public override void OnAttackPressed()
    {
        playerCombatController?.OnAttackPressed();
    }

    public override void OnAttackHeld()
    {
        playerCombatController?.OnAttackHeld();
    }

    // =====================================================
    // CÁMARA / VISIBILIDAD
    // =====================================================

    /// <summary>
    /// Desactiva todos los Renderer del jugador remoto en este cliente.
    /// Cada cliente ve solamente su propio personaje; los demás son invisibles.
    /// Los renderers siguen existiendo en el servidor (para colisiones, etc.)
    /// pero no se dibujan en la pantalla de este cliente.
    /// </summary>
    private void OcultarRenderersRemotos()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(includeInactive: true))
            r.enabled = false;

        Debug.Log($"[Player] Renderers ocultos para jugador remoto: {gameObject.name}");
    }
}