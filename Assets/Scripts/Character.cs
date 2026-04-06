using UnityEngine;
public abstract class Character : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] protected CharacterData characterData;
    protected CharacterStats stats;
    protected MovementController movementController;
    protected CombatController combatController;
    protected DamageReceiver damageReceiver;
    protected ResourceController resourceController;
    protected InventoryController inventoryController;
    protected EquipmentController equipmentController;
    protected TargetingController targetingController;
    public CharacterStats GetStats() => stats;
    public ResourceController GetResourceController() => resourceController;
    public InventoryController GetInventory() => inventoryController;
    public EquipmentController GetEquipment() => equipmentController;
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
        movementController ??= gameObject.AddComponent<MovementController>();
        combatController ??= gameObject.AddComponent<CombatController>();
        damageReceiver ??= gameObject.AddComponent<DamageReceiver>();
        resourceController ??= gameObject.AddComponent<ResourceController>();
        inventoryController ??= gameObject.AddComponent<InventoryController>();
        equipmentController ??= gameObject.AddComponent<EquipmentController>();
        targetingController ??= gameObject.AddComponent<TargetingController>();
        movementController.Initialize(this);
        combatController.Initialize(this);
        damageReceiver.Initialize(this);
        resourceController.Initialize(this);
        inventoryController.Initialize(this);
        equipmentController.Initialize(this);
        targetingController.Initialize(this);
    }
    protected virtual CharacterStats CreateStats()
    {
        return new CharacterStats(characterData);
    }
    #region Combat
    public virtual void Attack(Character target)
    {
        combatController?.Attack(target);
    }
    public virtual void SpecialAttack()
    {
        targetingController?.UpdateTarget();
        combatController?.SpecialAttack(targetingController?.CurrentTarget);
    }
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
        targetingController?.UpdateTarget();
        combatController?.OnAttackReleased();
    }
    public virtual void TakeDamage(float amount, Character attacker)
    {
        damageReceiver?.TakeDamage(amount, attacker);
    }
    protected virtual void OnDamaged(Character attacker) { }
    public void HandleDamaged(Character attacker)
    {
        OnDamaged(attacker);
    }
    protected virtual void Die()
    {
        Destroy(gameObject);
    }
    public void HandleDeath()
    {
        Die();
    }
    #endregion
    #region Resources
    public virtual void Heal(float amount)
    {
        resourceController?.Heal(amount);
    }
    public virtual bool UseMana(float amount)
    {
        return resourceController != null && resourceController.UseMana(amount);
    }
    public virtual void AddMana(float amount)
    {
        resourceController?.AddMana(amount);
    }
    #endregion
    #region Movement
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
    #endregion
    #region Getters
    public int GetLevel() => stats.Level;
    #endregion
}