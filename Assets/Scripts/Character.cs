using UnityEngine;
using Unity.Netcode;

public abstract class Character : NetworkBehaviour
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

      
        if (movementController == null)
            Debug.LogError($"[{nameof(Character)}] Falta MovementController en el prefab de {gameObject.name}");
        if (combatController == null)
            Debug.LogError($"[{nameof(Character)}] Falta CombatController en el prefab de {gameObject.name}");
        if (damageReceiver == null)
            Debug.LogError($"[{nameof(Character)}] Falta DamageReceiver en el prefab de {gameObject.name}");
        if (resourceController == null)
            Debug.LogError($"[{nameof(Character)}] Falta ResourceController en el prefab de {gameObject.name}");
        if (inventoryController == null)
            Debug.LogError($"[{nameof(Character)}] Falta InventoryController en el prefab de {gameObject.name}");
        if (equipmentController == null)
            Debug.LogError($"[{nameof(Character)}] Falta EquipmentController en el prefab de {gameObject.name}");
        if (targetingController == null)
            Debug.LogError($"[{nameof(Character)}] Falta TargetingController en el prefab de {gameObject.name}");

        movementController?.Initialize(this);
        combatController?.Initialize(this);
        damageReceiver?.Initialize(this);
        resourceController?.Initialize(this);
        inventoryController?.Initialize(this);
        equipmentController?.Initialize(this);
        targetingController?.Initialize(this);
    }

    protected virtual CharacterStats CreateStats()
    {
        return new CharacterStats(characterData);
    }

    #region Combat

    public virtual void Attack(Character target)
    {
        if (!IsServer) return;
        combatController?.Attack(target);
    }

    public virtual void SpecialAttack()
    {
        if (!IsServer) return;
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
        if (!IsServer) return;
        damageReceiver?.TakeDamage(amount, attacker);
    }

    protected virtual void OnDamaged(Character attacker) { }

    public void HandleDamaged(Character attacker)
    {
        OnDamaged(attacker);
    }

    protected virtual void Die()
    {
        if (!IsServer) return;

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

    public void HandleDeath()
    {
        Die();
    }

    #endregion

    #region Resources

   
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