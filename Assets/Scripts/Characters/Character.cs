using UnityEngine;
using Unity.Netcode;

public abstract class Character : NetworkBehaviour
{
    // =========================
    // DATA
    // =========================

    [Header("Data")]
    [SerializeField] protected CharacterData characterData;

    protected CharacterStats stats;

    // =========================
    // CONTROLLERS
    // =========================

    protected MovementController movementController;
    protected CombatController combatController;
    protected DamageReceiver damageReceiver;
    protected ResourceController resourceController;
    protected InventoryController inventoryController;
    protected EquipmentController equipmentController;
    protected TargetingController targetingController;

    // =========================
    // GETTERS
    // =========================

    public CharacterStats GetStats() => stats;
    public ResourceController GetResourceController() => resourceController;
    public InventoryController GetInventory() => inventoryController;
    public EquipmentController GetEquipment() => equipmentController;

    public int GetLevel() => stats.Level;

    // =========================
    // LIFECYCLE
    // =========================

    protected virtual void Awake()
    {
        stats = CreateStats();

        movementController = GetComponent<MovementController>();
        combatController = GetComponent<CombatController>();
        damageReceiver = GetComponent<DamageReceiver>();
        resourceController = GetComponent<ResourceController>();
        inventoryController = GetComponent<InventoryController>();
        equipmentController = GetComponent<EquipmentController>();
        targetingController = GetComponent<TargetingController>();

        // Validación fuerte
        ValidateComponent(movementController, nameof(MovementController));
        ValidateComponent(combatController, nameof(CombatController));
        ValidateComponent(damageReceiver, nameof(DamageReceiver));
        ValidateComponent(resourceController, nameof(ResourceController));
        ValidateComponent(inventoryController, nameof(InventoryController));
        ValidateComponent(equipmentController, nameof(EquipmentController));
        ValidateComponent(targetingController, nameof(TargetingController));

        // Inicialización
        movementController?.Initialize(this);
        combatController?.Initialize(this);
        damageReceiver?.Initialize(this);
        resourceController?.Initialize(this);
        inventoryController?.Initialize(this);
        equipmentController?.Initialize(this);
        targetingController?.Initialize(this);

        Debug.Log($"[Character.Awake] {gameObject.name} | SceneInstance: {gameObject.scene.IsValid()}");
    }

    protected virtual CharacterStats CreateStats()
    {
        return new CharacterStats(characterData);
    }

    private void ValidateComponent(Component comp, string name)
    {
        if (comp == null)
            Debug.LogError($"[Character] Falta {name} en {gameObject.name}");
    }

    // =========================
    // COMBAT (INPUT WRAPPER)
    // =========================

    public virtual void OnAttackPressed()
    {
        combatController?.OnAttackPressed();
    }

    public virtual void OnAttackHeld()
    {
        combatController?.OnAttackHeld(Time.deltaTime);
    }

    public virtual void OnAttackReleased()
    {
        combatController?.OnAttackReleased();
    }

    public virtual void SpecialAttack()
    {
        combatController?.SpecialAttack();
    }

    // =========================
    // DAMAGE FLOW (CORE SYSTEM)
    // =========================

    public void HandleDamaged(Character attacker)
    {
        Debug.Log($"[Character] {name} fue dañado por {attacker?.name}");
        OnDamaged(attacker);
    }

    public void HandleDeath()
    {
        Debug.Log($"[Character] {name} HandleDeath()");
        Die();
    }

    protected virtual void OnDamaged(Character attacker) { }

    // =========================
    // DEATH SYSTEM
    // =========================

    protected virtual void Die()
    {
        if (!IsServer) return;

        Debug.Log($"[Character] {name} murió");

        NotifyDeathClientRpc();

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
        else
            Destroy(gameObject);
    }

    [ClientRpc]
    protected virtual void NotifyDeathClientRpc()
    {
        OnDeath();
    }

    protected virtual void OnDeath() { }

    // =========================
    // RESOURCES
    // =========================

    public virtual void Heal(float amount)
    {
        if (!IsServer) return;
        resourceController?.Heal(amount);
    }

    public virtual bool UseMana(float amount)
    {
        if (!IsServer) return false;
        return resourceController != null && resourceController.UseMana(amount);
    }

    public virtual void AddMana(float amount)
    {
        if (!IsServer) return;
        resourceController?.AddMana(amount);
    }

    // =========================
    // MOVEMENT
    // =========================

    public virtual void Move(Vector3 direction)
    {
        movementController?.Move(direction);
    }

    public virtual void Run(Vector3 direction)
    {
        movementController?.Run(direction);
    }

    public virtual void Jump()
    {
        movementController?.Jump();
    }

    public virtual void ApplyGravity()
    {
        movementController?.ApplyGravity();
    }
}