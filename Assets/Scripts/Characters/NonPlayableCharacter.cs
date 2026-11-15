using UnityEngine;
using Unity.Netcode;

public class NonPlayableCharacter : Character
{
    // =========================
    // NETWORK
    // =========================

    private NetworkVariable<bool> isHostile = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // =========================
    // COMBAT
    // =========================

    protected CombatController combatController;

    private Character currentTarget;

    // =========================
    // LIFECYCLE
    // =========================

    protected override void Awake()
    {
        base.Awake();

        combatController = GetOptional<CombatController>();

        if (combatController != null)
            combatController.Initialize(this);
        else if (requireCombatComponents)
            Debug.LogError(
                $"[NonPlayableCharacter] Falta CombatController en {gameObject.name}");
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        isHostile.OnValueChanged += OnHostileStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        isHostile.OnValueChanged -= OnHostileStateChanged;
    }

    // =========================
    // HOSTILITY
    // =========================

    private void OnHostileStateChanged(bool previousValue, bool newValue)
    {
        if (newValue)
            OnBecomeHostile();
        else
            OnBecomePassive();
    }

    protected virtual void OnBecomeHostile() { }

    protected virtual void OnBecomePassive() { }

    // =========================
    // DAMAGE REACTION
    // =========================

    protected override void OnDamaged(Character attacker)
    {
        if (!IsServer) return;

        base.OnDamaged(attacker);

        if (!isHostile.Value)
        {
            isHostile.Value = true;
            currentTarget = attacker;
        }
    }

    // =========================
    // AI UPDATE
    // =========================

    private void Update()
    {
        if (!IsServer) return;

        if (!isHostile.Value) return;

        if (currentTarget == null) return;

        // Simular targeting para AI
        targetingController?.ForceTarget(currentTarget);

        combatController?.Attack();
    }

    // =========================
    // COMBAT API
    // =========================

    public override void OnAttackPressed()
    {
        combatController?.OnAttackPressed();
    }

    public override void OnAttackHeld()
    {
        combatController?.OnAttackHeld(Time.deltaTime);
    }

    public override void OnAttackReleased()
    {
        combatController?.OnAttackReleased();
    }

    public override void SpecialAttack()
    {
        combatController?.SpecialAttack();
    }
}