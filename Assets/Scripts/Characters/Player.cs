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

        if (!IsOwner) return;

        inputController?.Initialize(this);

        statsSync = GetComponent<CharacterStatsSyncController>();

        if (statsSync == null)
        {
            Debug.LogError("[Player] Falta CharacterStatsSyncController");
            return;
        }

        hud = FindFirstObjectByType<PlayerHUD>();

        if (hud == null)
        {
            Debug.LogError("[Player] No se encontro PlayerHUD");
            return;
        }

        inventoryUI = FindFirstObjectByType<InventoryUI>();

        if (inventoryUI == null)
        {
            Debug.LogWarning("[Player] No se encontro InventoryUI");
        }
        else
        {
            inventoryUI.Initialize(
                inventoryController,
                equipmentController,
                this);
        }

        StartCoroutine(InitializeHUDWhenReady());
    }

    private IEnumerator InitializeHUDWhenReady()
    {
        yield return new WaitUntil(
            () =>
                statsSync != null &&
                statsSync.NetMaxHealth.Value > 0);

        hud.Initialize(statsSync);
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

    public override void OnAttackReleased()
    {
        playerCombatController?.OnAttackReleased();
    }
}