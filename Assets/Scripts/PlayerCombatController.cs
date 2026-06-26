using UnityEngine;
using Unity.Netcode;

/// <summary>
/// PlayerCombatController 2.0
/// ------------------------------------------------------------------
/// Recupera las ventajas del CombatController viejo:
/// - Sistema basado en behaviors
/// - Hold attacks
/// - Multiplicadores por arma
/// - AttackSpeed real
/// - Validación de target
/// - SphereCast validation
/// - Sistema extensible
///
/// PERO:
/// - Sin tocar CombatController de enemies
/// - Compatible con ranged
/// - Compatible con magic
/// - Compatible con mana/stamina
/// - Separado exclusivamente para Player
/// </summary>
public class PlayerCombatController : NetworkBehaviour
{
    private Player player;

    private CharacterStats stats;
    private EquipmentController equipment;
    private ResourceController resources;
    private TargetingController targeting;

    [Header("Fallback")]
    [SerializeField] private float holdThreshold = 0.4f;
    [SerializeField] private float fallbackHeavyMultiplier = 2f;
    [SerializeField] private float fallbackHeavyStaminaCost = 25f;
    [SerializeField] private float fallbackMagicManaCost = 20f;

    [Header("Hit Validation")]
    [SerializeField] private float attackConeAngle = 60f;
    [SerializeField] private float sphereCastRadius = 0.8f;
    [SerializeField] private LayerMask enemyLayer;

    private float holdTimer;
    private bool isHoldingAttack;
    private bool heavyTriggered;

    // =========================================================
    // INIT
    // =========================================================

    public void Initialize(Player player)
    {
        this.player = player;

        stats = player.GetStats();
        equipment = player.GetEquipment();
        resources = player.GetResourceController();
        targeting = player.GetComponent<TargetingController>();
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private WeaponData GetCurrentWeapon()
    {
        return equipment?.GetEquippedWeapon();
    }

    private IWeaponBehavior GetCurrentBehavior()
    {
        WeaponData weapon = GetCurrentWeapon();
        return WeaponBehaviorFactory.Create(weapon, transform);
    }

    private float GetEffectiveHoldThreshold()
    {
        WeaponData weapon = GetCurrentWeapon();

        if (weapon == null || weapon.AttackSpeed <= 0f)
            return holdThreshold;

        return holdThreshold / weapon.AttackSpeed;
    }

    private float GetHeavyMultiplier()
    {
        WeaponData weapon = GetCurrentWeapon();

        return weapon != null
            ? weapon.HeavyMultiplier
            : fallbackHeavyMultiplier;
    }

    // =========================================================
    // INPUT
    // =========================================================

    public void OnAttackPressed()
    {
        if (!IsOwner)
            return;

        isHoldingAttack = true;
        heavyTriggered = false;
        holdTimer = 0f;
    }

    public void OnAttackHeld()
    {
        if (!IsOwner ||
            !isHoldingAttack ||
            heavyTriggered)
        {
            return;
        }

        holdTimer += Time.deltaTime;

        if (holdTimer >= GetEffectiveHoldThreshold())
        {
            heavyTriggered = true;
            isHoldingAttack = false;

            RequestHeavyAttackServerRpc();
        }
    }

    public void OnAttackReleased()
    {
        if (!IsOwner || !isHoldingAttack)
            return;

        isHoldingAttack = false;

        if (!heavyTriggered)
            RequestAttackServerRpc();
    }

    // =========================================================
    // RPCS
    // =========================================================

    [ServerRpc]
    private void RequestAttackServerRpc()
    {
        PerformAttack(false);
    }

    [ServerRpc]
    private void RequestHeavyAttackServerRpc()
    {
        PerformAttack(true);
    }

    [ServerRpc]
    public void RequestSpecialAttackServerRpc()
    {
        PerformSpecialAttack();
    }

    // =========================================================
    // CORE ATTACK
    // =========================================================

    private void PerformAttack(bool heavy)
    {
        Character target = FindTarget(); // Puede ser null

        // Si tu juego requiere obligatoriamente hit validation para hacer daño,
        // se la aplicamos SOLO si hay un target al que golpear.
        if (target != null && !ValidateHit(target))
        {
            Debug.LogWarning("[PlayerCombat] Hit validation failed");
            return;
        }

        if (!ValidateHit(target))
        {
            Debug.LogWarning(
                "[PlayerCombat] Hit validation failed");

            return;
        }

        WeaponData weapon = GetCurrentWeapon();

        // =====================================================
        // STAMINA COST FOR HEAVY
        // =====================================================

        if (heavy)
        {
            float staminaCost =
                weapon != null
                ? weapon.StaminaCost
                : fallbackHeavyStaminaCost;

            if (!resources.UseStamina(staminaCost))
            {
                Debug.Log(
                    "[PlayerCombat] Not enough stamina");

                return;
            }
        }

        // =====================================================
        // EXECUTE VIA BEHAVIOR
        // =====================================================

        IWeaponBehavior behavior = GetCurrentBehavior();

        if (heavy)
        {
            behavior.PerformHeavyAttack(
                player,
                target,
                GetHeavyMultiplier());
        }
        else
        {
            behavior.PerformAttack(
                player,
                target);
        }

        Debug.Log(
            $"[PlayerCombat] ATTACK {player.name} -> {target.name} | Heavy:{heavy}");
    }

    // =========================================================
    // SPECIAL ATTACK
    // =========================================================

    private void PerformSpecialAttack()
    {
        Character target = FindTarget();

        if (target == null)
        {
            Debug.LogWarning(
                "[PlayerCombat] No special target");

            return;
        }

        if (!ValidateHit(target))
        {
            Debug.LogWarning(
                "[PlayerCombat] Special validation failed");

            return;
        }

        WeaponData weapon = GetCurrentWeapon();

        float manaCost =
            weapon != null
            ? weapon.ManaCost
            : fallbackMagicManaCost;

        if (!resources.UseMana(manaCost))
        {
            Debug.Log(
                "[PlayerCombat] Not enough mana");

            return;
        }

        GetCurrentBehavior()
            .PerformSpecialAttack(player, target);

        Debug.Log(
            $"[PlayerCombat] SPECIAL {player.name} -> {target.name}");
    }

    // =========================================================
    // TARGETING
    // =========================================================

    private Character FindTarget()
    {
        Character target = targeting?.CurrentTarget;

        if (target != null)
            return target;

        return CombatTargetingSystem.FindBestTarget(
            player,
            stats.AttackRange.Value,
            attackConeAngle,
            enemyLayer);
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private bool ValidateHit(Character target)
    {
        WeaponData weapon = GetCurrentWeapon();

        // =====================================================
        // RANGED / MAGIC
        // No necesitan spherecast melee
        // =====================================================

        if (weapon != null && weapon.IsRanged)
            return true;

        // =====================================================
        // MELEE VALIDATION
        // =====================================================

        Vector3 origin =
            transform.position + Vector3.up;

        Vector3 targetPos =
            target.transform.position + Vector3.up;

        Vector3 direction =
            (targetPos - origin).normalized;

        float distance =
            Vector3.Distance(origin, targetPos);

        float castDistance = distance + 0.5f;

        if (Physics.SphereCast(
            origin,
            sphereCastRadius,
            direction,
            out RaycastHit hit,
            castDistance,
            enemyLayer,
            QueryTriggerInteraction.Collide))
        {
            Character hitCharacter =
                hit.collider.GetComponentInParent<Character>();

            return hitCharacter != null &&
                   hitCharacter == target;
        }

        return false;
    }

    // =========================================================
    // DEBUG
    // =========================================================

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