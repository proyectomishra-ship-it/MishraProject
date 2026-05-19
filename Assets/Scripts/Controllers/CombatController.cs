using UnityEngine;
using Unity.Netcode;

public class CombatController : NetworkBehaviour
{
    private Character character;
    private CharacterStats stats;
    private EquipmentController equipmentController;
    private TargetingController targetingController;

    [Header("Attack Hold")]
    [SerializeField]
    private float holdThreshold = 0.4f;

    [Header("Targeting")]
    [SerializeField]
    private float attackConeAngle = 60f;

    [SerializeField]
    private LayerMask enemyLayer;

    private float attackHeldTime;

    private bool isHoldingAttack;
    private bool heavyConsumed;

    // =========================
    // INITIALIZE
    // =========================

    public void Initialize(Character character)
    {
        this.character = character;

        stats =
            character.GetStats();

        equipmentController =
            character.GetEquipment();

        targetingController =
            character.GetComponent<TargetingController>();
    }

    // =========================
    // WEAPON BEHAVIOR
    // =========================

    private IWeaponBehavior GetCurrentBehavior()
    {
        WeaponData weapon =
            equipmentController
            ?.GetEquippedWeapon();

        return WeaponBehaviorFactory.Create(
            weapon,
            transform);
    }

    private float GetEffectiveHoldThreshold()
    {
        WeaponData weapon =
            equipmentController
            ?.GetEquippedWeapon();

        if (weapon == null)
            return holdThreshold;

        if (weapon.AttackSpeed <= 0f)
            return holdThreshold;

        return holdThreshold / weapon.AttackSpeed;
    }

    // =========================
    // INPUT API
    // =========================

    public void OnAttackPressed()
    {
        if (!IsServer)
            return;

        isHoldingAttack = true;

        attackHeldTime = 0f;

        heavyConsumed = false;
    }

    public void OnAttackHeld(float deltaTime)
    {
        if (!IsServer)
            return;

        if (!isHoldingAttack)
            return;

        if (heavyConsumed)
            return;

        attackHeldTime += deltaTime;

        if (attackHeldTime >= GetEffectiveHoldThreshold())
        {
            ExecuteAttack(true);

            heavyConsumed = true;

            isHoldingAttack = false;
        }
    }

    public void OnAttackReleased()
    {
        if (!IsServer)
            return;

        if (!isHoldingAttack)
            return;

        isHoldingAttack = false;

        if (!heavyConsumed)
        {
            ExecuteAttack(false);
        }
    }

    // =========================
    // PUBLIC API
    // =========================

    public void ExecuteAttack(bool heavy = false)
    {
        if (!IsServer)
            return;

        Character target =
            FindTarget();

        if (target == null)
        {
            Debug.LogWarning(
                $"[Combat] {character.name}: no target");

            return;
        }

        IWeaponBehavior behavior =
            GetCurrentBehavior();

        if (behavior == null)
        {
            Debug.LogWarning(
                $"[Combat] {character.name}: no weapon behavior");

            return;
        }

        behavior.ExecuteAttack(
            character,
            target,
            heavy);
    }

    public void ExecuteSpecialAttack()
    {
        if (!IsServer)
            return;

        Character target =
            FindTarget();

        if (target == null)
        {
            Debug.LogWarning(
                $"[Combat] {character.name}: no target");

            return;
        }

        IWeaponBehavior behavior =
            GetCurrentBehavior();

        if (behavior == null)
        {
            Debug.LogWarning(
                $"[Combat] {character.name}: no weapon behavior");

            return;
        }

        behavior.ExecuteSpecialAttack(
            character,
            target);
    }

    // =========================
    // TARGETING
    // =========================

    private Character FindTarget()
    {
        Character target =
            targetingController
            ?.CurrentTarget;

        if (target != null)
            return target;

        return CombatTargetingSystem.FindBestTarget(
            character,
            stats.AttackRange.Value,
            attackConeAngle,
            enemyLayer);
    }

    // =========================
    // DEBUG
    // =========================

    private void OnDrawGizmosSelected()
    {
        if (stats == null)
            return;

        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            stats.AttackRange.Value);
    }
}