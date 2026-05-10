using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class Player : Character
{
    [SerializeField] private PlayerClassData classData;

    private PlayerHUD hud;
    private CharacterStatsSyncController statsSync;
    private PlayerInputController inputController;

    // =========================
    // UNITY
    // =========================

    protected override void Awake()
    {
        base.Awake();

        inputController = GetComponent<PlayerInputController>();

        if (inputController == null)
        {
            Debug.LogError(
                $"[Player] Falta PlayerInputController en {gameObject.name}"
            );
        }
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
            Debug.LogError("[Player] No se encontró PlayerHUD en la escena");
            return;
        }

        StartCoroutine(InitializeHUDWhenReady());
    }

    // =========================
    // HUD INIT
    // =========================

    private IEnumerator InitializeHUDWhenReady()
    {
        Debug.Log("[Player] Esperando sync del HUD...");

        yield return new WaitUntil(() =>
            statsSync != null &&
            statsSync.NetMaxHealth.Value > 0
        );

        Debug.Log("[Player] HUD listo");

        hud.Initialize(statsSync);
    }

    // =========================
    // STATS
    // =========================

    protected override CharacterStats CreateStats()
    {
        return new PlayerStats(characterData, classData);
    }

    public void AddExp(int amount)
    {
        if (!IsServer) return;

        ((PlayerStats)stats).AddExperience(amount);
    }

    // =========================
    // MOVEMENT
    // =========================

    public override void Move(Vector3 direction)
    {
        if (IsOwner)
            MoveServerRpc(direction);
    }

    public override void Run(Vector3 direction)
    {
        if (IsOwner)
            RunServerRpc(direction);
    }

    public override void Jump()
    {
        if (IsOwner)
            JumpServerRpc();
    }

    public override void ApplyGravity()
    {
        if (IsOwner)
            ApplyGravityServerRpc();
    }

    [ServerRpc]
    private void MoveServerRpc(Vector3 direction)
    {
        base.Move(direction);
    }

    [ServerRpc]
    private void RunServerRpc(Vector3 direction)
    {
        base.Run(direction);
    }

    [ServerRpc]
    private void JumpServerRpc()
    {
        base.Jump();
    }

    [ServerRpc]
    private void ApplyGravityServerRpc()
    {
        base.ApplyGravity();
    }

    // =========================
    // COMBAT
    // =========================

    public override void OnAttackPressed()
    {
        if (IsOwner)
            OnAttackPressedServerRpc();
    }

    public override void OnAttackHeld()
    {
        if (IsOwner)
            OnAttackHeldServerRpc();
    }

    public override void OnAttackReleased()
    {
        if (IsOwner)
            OnAttackReleasedServerRpc();
    }

    public override void SpecialAttack()
    {
        if (IsOwner)
            SpecialAttackServerRpc();
    }

    [ServerRpc]
    private void OnAttackPressedServerRpc()
    {
        base.OnAttackPressed();
    }

    [ServerRpc]
    private void OnAttackHeldServerRpc()
    {
        base.OnAttackHeld();
    }

    [ServerRpc]
    private void OnAttackReleasedServerRpc()
    {
        base.OnAttackReleased();
    }

    [ServerRpc]
    private void SpecialAttackServerRpc()
    {
        base.SpecialAttack();
    }
}