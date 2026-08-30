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

    private GoldController goldController;

    private InventoryUI inventoryUI;

    // =====================================================
    // LIFECYCLE
    // =====================================================

    protected override void Awake()
    {
        base.Awake();

        inputController = GetComponent<PlayerInputController>();
        movementController = GetComponent<MovementController>();
        playerCombatController = GetComponent<PlayerCombatController>();
        goldController = GetComponent<GoldController>();

        if (goldController == null)
            Debug.LogError("[Player] Falta GoldController");

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
        {
            // =================================================
            // ARMA INICIAL
            // =================================================

            // Cualquier excepción acá (ej: ItemDatabase mal inicializado)
            // queda contenida para evitar que OnNetworkSpawn()
            // termine prematuramente y saltee la inicialización
            // del HUD, inventario, input, etc.
            try
            {
                EquiparArmaInicial();
            }
            catch (System.Exception e)
            {
                Debug.LogError(
                    $"[Player] EquiparArmaInicial falló: " +
                    $"{e.Message}\n{e.StackTrace}"
                );
            }
        }

        if (!IsOwner)
        {
            // Jugador remoto:
            // ocultar todos sus renderers en este cliente.
            OcultarRenderersRemotos();
            return;
        }

        inputController?.Initialize(this);

        statsSync = GetComponent<CharacterStatsSyncController>();

        if (statsSync == null)
        {
            Debug.LogError(
                "[Player] Falta CharacterStatsSyncController"
            );

            return;
        }

        inventoryUI = FindFirstObjectByType<InventoryUI>();

        if (inventoryUI == null)
            Debug.LogWarning(
                "[Player] No se encontro InventoryUI"
            );

        // Inicializar HUD e inventario en coroutine para evitar
        // problemas de timing al cargar la escena.
        StartCoroutine(InitializeWhenReady());
    }

    // =====================================================
    // GOLD
    // =====================================================

    /// <summary>
    /// Cantidad actual de oro del jugador.
    /// El valor real es mantenido por GoldController.
    /// </summary>
    public int GetGold()
    {
        return goldController != null
            ? goldController.Gold
            : 0;
    }

    /// <summary>
    /// Agrega oro al jugador.
    /// GoldController verifica que la operación se ejecute
    /// en el servidor.
    /// </summary>
    public bool AddGold(int amount)
    {
        if (goldController == null)
        {
            Debug.LogError(
                $"[Player] No se puede agregar oro a {name}: " +
                "GoldController es null."
            );

            return false;
        }

        return goldController.AddGold(amount);
    }

    /// <summary>
    /// Intenta quitar oro del jugador.
    /// Devuelve false si no tiene suficiente.
    /// </summary>
    public bool RemoveGold(int amount)
    {
        if (goldController == null)
        {
            Debug.LogError(
                $"[Player] No se puede quitar oro a {name}: " +
                "GoldController es null."
            );

            return false;
        }

        return goldController.RemoveGold(amount);
    }

    /// <summary>
    /// Comprueba si el jugador posee determinada cantidad de oro.
    /// </summary>
    public bool HasGold(int amount)
    {
        return goldController != null &&
               goldController.HasGold(amount);
    }

    // =====================================================
    // ARMA INICIAL POR CLASE
    // =====================================================

    private void EquiparArmaInicial()
    {
        Debug.Log(
            $"[Player] >>> EquiparArmaInicial() INICIO — " +
            $"GameObject: '{gameObject.name}'"
        );

        if (classData == null)
        {
            Debug.LogError(
                $"[Player] >>> ABORTA en '{gameObject.name}': " +
                "el campo 'Class Data' está vacío en el Inspector " +
                "del prefab."
            );

            return;
        }

        if (classData.StartingWeapon == null)
        {
            Debug.LogError(
                $"[Player] >>> ABORTA en '{gameObject.name}': " +
                $"classData ('{classData.name}') no tiene " +
                "'Starting Weapon' asignado."
            );

            return;
        }

        if (equipmentController == null)
        {
            Debug.LogError(
                $"[Player] >>> ABORTA en '{gameObject.name}': " +
                "equipmentController es null (falta el componente " +
                "EquipmentController en este GameObject, o " +
                "Character.Awake() no corrió antes que esto)."
            );

            return;
        }

        if (equipmentController.IsOccupied(EquipmentSlot.Weapon))
        {
            Debug.LogWarning(
                $"[Player] >>> ABORTA en '{gameObject.name}': " +
                "el slot de arma ya estaba ocupado " +
                "(¿EquiparArmaInicial se llamó dos veces?)."
            );

            return;
        }

        Debug.Log(
            $"[Player] >>> classData='{classData.name}' → " +
            $"arma='{classData.StartingWeapon.ItemName}'. " +
            "Agregando al inventario y equipando..."
        );

        // Agregar primero al inventario para que el arma exista
        // realmente como item y pueda regresar al inventario
        // cuando sea desequipada.
        bool added =
            inventoryController != null &&
            inventoryController.AddItem(
                classData.StartingWeapon,
                1
            );

        if (!added)
        {
            Debug.LogWarning(
                $"[Player] >>> No se pudo agregar " +
                $"'{classData.StartingWeapon.ItemName}' al inventario " +
                "(inventoryController null, o inventario lleno). " +
                "Se equipará igual, pero no aparecerá en la mochila."
            );
        }
        else
        {
            Debug.Log(
                $"[Player] >>> " +
                $"'{classData.StartingWeapon.ItemName}' " +
                "agregado al inventario OK."
            );
        }

        bool ok =
            equipmentController.Equip(
                classData.StartingWeapon
            );

        Debug.Log(
            $"[Player] >>> Arma inicial " +
            $"'{classData.StartingWeapon.ItemName}': " +
            $"{(ok ? "equipada OK" : "FALLÓ — verificar ItemDatabase.Instance")}"
        );
    }

    // =====================================================
    // INITIALIZATION
    // =====================================================

    private IEnumerator InitializeWhenReady()
    {
        // Esperar a que las estadísticas hayan sido sincronizadas.
        // Timeout para evitar bloqueo indefinido.
        float timeout = 10f;
        float elapsed = 0f;

        while (
            statsSync.NetMaxHealth.Value <= 0 &&
            elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (statsSync.NetMaxHealth.Value <= 0)
        {
            Debug.LogWarning(
                "[Player] NetMaxHealth nunca superó 0 tras 10s. " +
                "Verificá que CharacterData.MaxHealth > 0 y que " +
                "haya un host/client activo."
            );
        }

        // Buscar HUD con reintentos por si la escena todavía está cargando.
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
            Debug.LogError(
                "[Player] PlayerHUD no encontrado después de esperar."
            );
        }
        else
        {
            hud.Initialize(statsSync);
        }

        // Esperar un frame extra para que el transform
        // esté en su posición final.
        yield return null;

        // Inicializar el inventario después para que la cámara
        // de preview reciba el transform correctamente posicionado.
        if (inventoryUI != null)
        {
            inventoryUI.Initialize(
                inventoryController,
                equipmentController,
                this
            );
        }
    }

    // =====================================================
    // STATS
    // =====================================================

    protected override CharacterStats CreateStats()
    {
        return new PlayerStats(
            characterData,
            classData
        );
    }

    public void AddExp(int amount)
    {
        if (!IsServer)
            return;

        ((PlayerStats)stats).AddExperience(amount);
    }

    // =====================================================
    // INVENTORY / EQUIPMENT API
    // Llamados desde InventoryUI en el cliente local.
    // =====================================================

    public void RequestEquip(int itemId)
    {
        if (IsOwner)
            EquipServerRpc(itemId);
    }

    public void RequestUnequip(EquipmentSlot slot)
    {
        if (IsOwner)
            UnequipServerRpc((int)slot);
    }

    [ServerRpc]
    private void EquipServerRpc(int itemId)
    {
        var item = ItemDatabase.Instance.Get(itemId);

        if (item is not IEquippable equippable)
            return;

        bool ok = equipmentController.Equip(equippable);

        Debug.Log(
            $"[Player] Equipado '{item.ItemName}': {ok}"
        );
    }

    [ServerRpc]
    private void UnequipServerRpc(int slotIndex)
    {
        bool ok =
            equipmentController.Unequip(
                (EquipmentSlot)slotIndex
            );

        Debug.Log(
            $"[Player] Desequipado slot " +
            $"{(EquipmentSlot)slotIndex}: {ok}"
        );
    }

    // =====================================================
    // MOVEMENT
    // =====================================================

    public void Move(
        Vector3 worldDirection,
        Quaternion rotation)
    {
        if (IsOwner)
            MoveServerRpc(
                worldDirection,
                rotation
            );
    }

    public void Run(
        Vector3 worldDirection,
        Quaternion rotation)
    {
        if (IsOwner)
            RunServerRpc(
                worldDirection,
                rotation
            );
    }

    public void Stop()
    {
        if (IsOwner)
            StopServerRpc();
    }

    /// <summary>
    /// Bloquea o desbloquea el input del jugador
    /// (movimiento y ataque).
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
            rotation
        );
    }

    [ServerRpc]
    private void RunServerRpc(
        Vector3 worldDirection,
        Quaternion rotation)
    {
        movementController?.Run(
            worldDirection,
            rotation
        );
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

    public override void OnAttackReleased()
    {
        playerCombatController?.OnAttackReleased();
    }

    // =====================================================
    // CÁMARA / VISIBILIDAD
    // =====================================================

    /// <summary>
    /// Desactiva todos los Renderer del jugador remoto
    /// en este cliente.
    /// </summary>
    private void OcultarRenderersRemotos()
    {
        foreach (
            Renderer r in GetComponentsInChildren<Renderer>(
                includeInactive: true))
        {
            r.enabled = false;
        }

        Debug.Log(
            $"[Player] Renderers ocultos para jugador remoto: " +
            $"{gameObject.name}"
        );
    }
}