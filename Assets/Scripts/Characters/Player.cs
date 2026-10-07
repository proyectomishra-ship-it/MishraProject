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
    private CraftingController craftingController;

    private InventoryUI inventoryUI;

    private PlayerRespawnController respawnController;
    private PlayerAnimationController animationController;
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
        craftingController = GetComponent<CraftingController>();
        respawnController = GetComponent<PlayerRespawnController>();
        animationController = GetComponent<PlayerAnimationController>();

        if (goldController == null)
            Debug.LogError("[Player] Falta GoldController");

        if (inputController == null)
            Debug.LogError("[Player] Falta PlayerInputController");

        if (movementController == null)
            Debug.LogError("[Player] Falta MovementController");

        if (playerCombatController == null)
            Debug.LogError("[Player] Falta PlayerCombatController");

        if (craftingController == null)
            Debug.LogError("[Player] Falta CraftingController");

        if (respawnController == null) Debug.LogError(
                $"[Player] {name} no tiene PlayerRespawnController.");


        movementController?.Initialize(this);
        playerCombatController?.Initialize(this);
        craftingController?.Initialize(this);
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
            return;

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

        // Agregar primero al inventario: la mochila es la lista de todo lo
        // que el jugador posee, y el equipamiento solo marca cuál item está
        // en uso (equipar/desequipar no mueve el item de la mochila).
        // EquipServerRpc exige que el item esté acá para poder equiparlo.
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

        // El servidor es la autoridad: solo se puede equipar un item que
        // realmente esté en la mochila de este jugador. Sin esto, un cliente
        // modificado podría enviar cualquier id de ItemDatabase y equiparse
        // un item que no posee. La UI ya solo ofrece items de la mochila,
        // así que el uso normal no se ve afectado.
        if (inventoryController == null || !inventoryController.HasItem(item))
        {
            Debug.LogWarning(
                $"[Player] {name} intentó equipar '{item.ItemName}' " +
                "sin tenerlo en el inventario. Ignorado."
            );

            return;
        }

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
    // CRAFTING
    // Llamado desde InventoryUI (pestaña Crafteo) en el cliente local.
    // =====================================================

    /// <summary>
    /// Se dispara en el cliente dueño con el resultado de cada intento de
    /// crafteo (éxito o el motivo del fallo). InventoryUI se suscribe para
    /// mostrar feedback ("faltan materiales", etc.).
    /// </summary>
    public event System.Action<int, CraftResult> OnCraftResult;

    public void RequestCraft(int recipeId)
    {
        if (IsOwner)
            CraftServerRpc(recipeId);
    }

    [ServerRpc]
    private void CraftServerRpc(int recipeId)
    {
        var recipe = CraftingRecipeDatabase.Instance != null
            ? CraftingRecipeDatabase.Instance.Get(recipeId)
            : null;

        if (recipe == null)
        {
            Debug.LogWarning(
                $"[Player] CraftServerRpc: receta {recipeId} no encontrada."
            );

            NotifyCraftResultClientRpc(recipeId, CraftResult.InvalidRecipe, OwnerOnly());
            return;
        }

        CraftResult result = craftingController != null
            ? craftingController.TryCraft(recipe)
            : CraftResult.InvalidRecipe;

        Debug.Log(
            $"[Player] Crafteo receta {recipeId} ('{recipe.name}'): {result}"
        );

        NotifyCraftResultClientRpc(recipeId, result, OwnerOnly());
    }

    // El resultado solo le importa al dueño: no lo mandamos al resto de clientes.
    private ClientRpcParams OwnerOnly() => new ClientRpcParams
    {
        Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
    };

    [ClientRpc]
    private void NotifyCraftResultClientRpc(int recipeId, CraftResult result, ClientRpcParams rpcParams = default)
    {
        // Solo le importa al dueño local; los demás clientes lo ignoran.
        if (!IsOwner) return;

        OnCraftResult?.Invoke(recipeId, result);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // =====================================================
    // DEBUG (solo editor / development build)
    // F9 en el inventario → +5 de cada ItemType.Material.
    // Sirve para probar el crafting sin depender de los drops.
    // =====================================================

    public void DebugRequestMaterials()
    {
        if (IsOwner)
            DebugGiveMaterialsServerRpc();
    }

    [ServerRpc]
    private void DebugGiveMaterialsServerRpc()
    {
        var db  = ItemDatabase.Instance;
        var inv = GetInventory();
        if (db == null || inv == null) return;

        for (int id = 0; id < 256; id++)
        {
            var item = db.Get(id);
            if (item != null && item.ItemType == ItemType.Material)
                inv.AddItem(item, 5);
        }
    }
#endif

    // =====================================================
    // CONSUMIBLES (pociones, etc.)
    // Llamado desde InventoryUI (botón "Usar") en el cliente local.
    // =====================================================

    /// <summary>Resultado del último intento de usar un consumible (solo dueño).</summary>
    public event System.Action<ConsumeResult> OnConsumeResult;

    public void RequestUseItem(int itemId)
    {
        if (IsOwner)
            UseItemServerRpc(itemId);
    }

    [ServerRpc]
    private void UseItemServerRpc(int itemId)
    {
        var item = ItemDatabase.Instance != null
            ? ItemDatabase.Instance.Get(itemId)
            : null;

        ConsumeResult result = IsDead()
            ? ConsumeResult.PlayerDead
            : ConsumableSystem.TryUse(GetInventory(), GetResourceController(), item);

        Debug.Log($"[Player] Usar item {itemId} ('{(item != null ? item.name : "?")}'): {result}");

        var target = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        NotifyConsumeResultClientRpc(result, target);
    }

    [ClientRpc]
    private void NotifyConsumeResultClientRpc(ConsumeResult result, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;
        OnConsumeResult?.Invoke(result);
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

    /// <summary>
    /// Click derecho. Character.SpecialAttack() esta vacio y Player no lo
    /// sobreescribia, asi que el ataque especial del jugador nunca llegaba al
    /// servidor. RequestSpecialAttackServerRpc ya existia en PlayerCombatController.
    /// </summary>
    public override void SpecialAttack()
    {
        if (IsOwner)
            playerCombatController?.RequestSpecialAttackServerRpc();
    }

    /// <summary>
    /// DamageReceiver.TakeDamage lo llama en el servidor. Dispara la animacion de golpe.
    /// </summary>
    protected override void OnDamaged(Character attacker)
    {
        if (animationController != null)
            animationController.PlayHit();
    }

    /// <summary>
    /// Inicia el proceso de muerte y respawn del Player.
    /// Solo tiene efecto en el servidor.
    /// </summary>
    public void StartRespawn()
    {
        if (!IsServer)
            return;

        if (respawnController == null)
        {
            Debug.LogError(
                $"[Player] {name} no puede iniciar respawn: "
                + "PlayerRespawnController es null."
            );
            return;
        }

        respawnController.StartDeath();
    }

    /// <summary>
    /// Indica si el Player está actualmente muerto.
    /// </summary>
    public bool IsDead()
    {
        return respawnController != null && respawnController.IsDead.Value;
    }

    /// <summary>
    /// Indica si el Player está protegido contra daño.
    /// </summary>
    public bool IsInvulnerable()
    {
        return respawnController != null && respawnController.IsInvulnerable.Value;
    }
    protected override void Die()
    {
        if (!IsServer)
            return;

        Debug.Log($"[Player] {name} murió. Iniciando respawn.");

        StartRespawn();
    }
}