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
    private InventoryUI inventoryUI;

    protected override void Awake()
    {
        base.Awake();

        inputController = GetComponent<PlayerInputController>();
        movementController = GetComponent<MovementController>();

        if (inputController == null)
            Debug.LogError($"[Player] Falta PlayerInputController en {gameObject.name}");

        if (movementController == null)
            Debug.LogError($"[Player] Falta MovementController en {gameObject.name}");
        else
            movementController.Initialize(this);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsOwner) return;

        inputController?.Initialize(this);

        statsSync = GetComponent<CharacterStatsSyncController>();
        if (statsSync == null) { Debug.LogError("[Player] Falta CharacterStatsSyncController"); return; }

        hud = FindFirstObjectByType<PlayerHUD>();
        if (hud == null) { Debug.LogError("[Player] No se encontro PlayerHUD"); return; }

        inventoryUI = FindFirstObjectByType<InventoryUI>();
        if (inventoryUI == null)
            Debug.LogWarning("[Player] No se encontro InventoryUI en la escena.");
        else
            inventoryUI.Initialize(inventoryController, equipmentController);

        StartCoroutine(InitializeHUDWhenReady());
    }

    private IEnumerator InitializeHUDWhenReady()
    {
        yield return new WaitUntil(() => statsSync != null && statsSync.NetMaxHealth.Value > 0);
        hud.Initialize(statsSync);
    }

    protected override CharacterStats CreateStats() => new PlayerStats(characterData, classData);

    public void AddExp(int amount)
    {
        if (!IsServer) return;
        ((PlayerStats)stats).AddExperience(amount);
    }

    // =========================
    // MOVEMENT
    // =========================

    public void Move(Vector3 worldDirection, Quaternion rotation)
    {
        if (IsOwner) MoveServerRpc(worldDirection, rotation);
    }

    public void Run(Vector3 worldDirection, Quaternion rotation)
    {
        if (IsOwner) RunServerRpc(worldDirection, rotation);
    }

    public void Jump()
    {
        if (IsOwner) JumpServerRpc();
    }

    public void ApplyGravity()
    {
        if (IsOwner) ApplyGravityServerRpc();
    }

    public void SyncCameraYaw(float yaw) { }

    [ServerRpc]
    private void MoveServerRpc(Vector3 worldDirection, Quaternion rotation)
        => movementController?.Move(worldDirection, rotation);

    [ServerRpc]
    private void RunServerRpc(Vector3 worldDirection, Quaternion rotation)
        => movementController?.Run(worldDirection, rotation);

    [ServerRpc]
    private void JumpServerRpc()
        => movementController?.Jump();

    [ServerRpc]
    private void ApplyGravityServerRpc()
        => movementController?.ApplyGravity();

    // =========================
    // COMBAT
    // =========================

    public override void OnAttackPressed() { if (IsOwner) OnAttackPressedServerRpc(); }
    public override void OnAttackHeld() { if (IsOwner) OnAttackHeldServerRpc(); }
    public override void OnAttackReleased() { if (IsOwner) OnAttackReleasedServerRpc(); }
    public override void SpecialAttack() { if (IsOwner) SpecialAttackServerRpc(); }

    [ServerRpc] private void OnAttackPressedServerRpc() => base.OnAttackPressed();
    [ServerRpc] private void OnAttackHeldServerRpc() => base.OnAttackHeld();
    [ServerRpc] private void OnAttackReleasedServerRpc() => base.OnAttackReleased();
    [ServerRpc] private void SpecialAttackServerRpc() => base.SpecialAttack();
}