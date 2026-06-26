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

        if (IsServer)
            EquiparArmaInicial();

        if (!IsOwner)
        {
            // Jugador remoto: ocultar todos los renderers (Skinned, Mesh, etc.)
            // para que no aparezcan en la cámara del jugador local.
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

    // =====================================================
    // ARMA INICIAL POR CLASE
    // =====================================================

    private void EquiparArmaInicial()
    {
        if (classData == null || classData.StartingWeapon == null) return;
        if (equipmentController == null) return;
        if (equipmentController.IsOccupied(EquipmentSlot.Weapon)) return;

        bool ok = equipmentController.Equip(classData.StartingWeapon);
        Debug.Log($"[Player] Arma inicial '{classData.StartingWeapon.ItemName}': " +
                  $"{(ok ? "equipada" : "falló — verificar ItemDatabase")}");
    }

    private IEnumerator InitializeWhenReady()
    {
        // FIX BUG 1: WaitUntil con timeout para evitar bloqueo indefinido.
        // Si CharacterData.MaxHealth es 0 o el juego corre sin networking,
        // NetMaxHealth.Value nunca supera 0 y la coroutine quedaba bloqueada,
        // impidiendo que el inventario se inicializara.
        float timeout = 10f;
        float elapsed = 0f;
        while (statsSync.NetMaxHealth.Value <= 0 && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (statsSync.NetMaxHealth.Value <= 0)
            Debug.LogWarning("[Player] NetMaxHealth nunca superó 0 tras 10s. " +
                             "Verificá que CharacterData.MaxHealth > 0 y que haya un host/client activo.");

        // Buscar HUD con reintentos por si la escena aún está cargando
        float hudTimeout = 5f;
        float hudElapsed = 0f;
        while (hud == null && hudElapsed < hudTimeout)
        {
            hud = FindFirstObjectByType<PlayerHUD>();
            hudElapsed += Time.deltaTime;
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
    /// </summary>
    private void OcultarRenderersRemotos()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(includeInactive: true))
            r.enabled = false;

        Debug.Log($"[Player] Renderers ocultos para jugador remoto: {gameObject.name}");
    }
}