using UnityEngine;
using Unity.Netcode;

public class PlayerCombatController : NetworkBehaviour
{
    private Player player;

    private CharacterStats stats;
    private EquipmentController equipment;
    private ResourceController resources;
    private TargetingController targeting;

    [Header("Heavy Attack")]
    [SerializeField] private float holdThreshold = 0.4f;
    [SerializeField] private float fallbackHeavyStaminaCost = 25f;

    [Header("Magic")]
    [SerializeField] private float fallbackMagicManaCost = 20f;

    [Header("Validation")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float meleeRadius = 1.5f;

    private bool isHoldingAttack;
    private bool heavyTriggered;
    private float holdTimer;

    public void Initialize(Player player)
    {
        this.player = player;

        stats = player.GetStats();
        equipment = player.GetEquipment();
        resources = player.GetResourceController();
        targeting = player.GetComponent<TargetingController>();
    }

    // =========================================================
    // INPUT
    // =========================================================

    public void OnAttackPressed()
    {
        if (!IsOwner) return;

        isHoldingAttack = true;
        heavyTriggered = false;
        holdTimer = 0f;
    }

    public void OnAttackHeld()
    {
        if (!IsOwner || !isHoldingAttack || heavyTriggered)
            return;

        holdTimer += Time.deltaTime;

        if (holdTimer >= holdThreshold)
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

        WeaponData weapon = equipment.GetEquippedWeapon();

        if (weapon == null)
        {
            RequestMeleeAttackServerRpc();
            return;
        }

        switch (weapon.WeaponType)
        {
            case WeaponType.Bow:
                RequestRangedAttackServerRpc();
                break;

            case WeaponType.Staff:
            case WeaponType.Grimoire:
                RequestMagicAttackServerRpc();
                break;

            default:
                RequestMeleeAttackServerRpc();
                break;
        }
    }

    // =========================================================
    // SERVER RPCS
    // =========================================================

    [ServerRpc]
    private void RequestMeleeAttackServerRpc()
    {
        PerformMeleeAttack(false);
    }

    [ServerRpc]
    private void RequestHeavyAttackServerRpc()
    {
        PerformHeavyAttack();
    }

    [ServerRpc]
    private void RequestRangedAttackServerRpc()
    {
        PerformRangedAttack();
    }

    [ServerRpc]
    private void RequestMagicAttackServerRpc()
    {
        PerformMagicAttack();
    }

    // =========================================================
    // MELEE
    // =========================================================

    private void PerformMeleeAttack(bool heavy)
    {
        Character target = targeting.CurrentTarget;

        if (target == null)
        {
            Debug.Log("[PlayerCombat] No target");
            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            target.transform.position);

        if (distance > meleeRadius)
        {
            Debug.Log("[PlayerCombat] Target out of melee range");
            return;
        }

        float damage = stats.Attack.Value;

        if (heavy)
        {
            WeaponData weapon =
                equipment.GetEquippedWeapon();

            float multiplier =
                weapon != null
                ? weapon.HeavyMultiplier
                : 2f;

            damage *= multiplier;
        }

        target.GetComponent<DamageReceiver>()
            ?.TakeDamage(damage, player);

        Debug.Log(
            $"[PlayerCombat] MELEE HIT {target.name}");
    }

    // =========================================================
    // HEAVY
    // =========================================================

    private void PerformHeavyAttack()
    {
        WeaponData weapon =
            equipment.GetEquippedWeapon();

        float staminaCost =
            weapon != null
            ? weapon.StaminaCost
            : fallbackHeavyStaminaCost;

        if (!resources.UseResistance(staminaCost))
        {
            Debug.Log("[PlayerCombat] Not enough stamina");
            return;
        }

        PerformMeleeAttack(true);
    }

    // =========================================================
    // RANGED
    // =========================================================

    private void PerformRangedAttack()
    {
        WeaponData weapon =
            equipment.GetEquippedWeapon();

        if (weapon == null ||
            weapon.ProjectilePrefab == null)
        {
            Debug.LogWarning(
                "[PlayerCombat] Invalid ranged weapon");

            return;
        }

        Character target = targeting.CurrentTarget;

        if (target == null)
        {
            Debug.Log(
                "[PlayerCombat] No ranged target");

            return;
        }

        Vector3 origin =
            transform.position + Vector3.up * 1.5f;

        Vector3 targetPos =
            target.transform.position + Vector3.up;

        Vector3 dir =
            (targetPos - origin).normalized;

        GameObject obj = Instantiate(
            weapon.ProjectilePrefab,
            origin,
            Quaternion.LookRotation(dir));

        if (!obj.TryGetComponent(
            out NetworkObject netObj))
        {
            Destroy(obj);
            return;
        }

        if (!obj.TryGetComponent(
            out NetworkProjectile projectile))
        {
            Destroy(obj);
            return;
        }

        netObj.Spawn();

        projectile.Initialize(
            player,
            stats.Attack.Value,
            dir);

        Debug.Log(
            $"[PlayerCombat] RANGED SHOT {target.name}");
    }

    // =========================================================
    // MAGIC
    // =========================================================

    private void PerformMagicAttack()
    {
        WeaponData weapon =
            equipment.GetEquippedWeapon();

        if (weapon == null)
            return;

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

        Character target = targeting.CurrentTarget;

        if (target == null)
        {
            Debug.Log(
                "[PlayerCombat] No magic target");

            return;
        }

        GameObject projectilePrefab =
            weapon.GetSpecialProjectile();

        if (projectilePrefab == null)
        {
            Debug.LogWarning(
                "[PlayerCombat] Missing magic projectile");

            return;
        }

        Vector3 origin =
            transform.position + Vector3.up * 1.5f;

        Vector3 targetPos =
            target.transform.position + Vector3.up;

        Vector3 dir =
            (targetPos - origin).normalized;

        GameObject obj = Instantiate(
            projectilePrefab,
            origin,
            Quaternion.LookRotation(dir));

        if (!obj.TryGetComponent(
            out NetworkObject netObj))
        {
            Destroy(obj);
            return;
        }

        if (!obj.TryGetComponent(
            out NetworkProjectile projectile))
        {
            Destroy(obj);
            return;
        }

        float magicDamage =
            stats.Attack.Value +
            stats.Intelligence.Value * 1.5f;

        netObj.Spawn();

        projectile.Initialize(
            player,
            magicDamage,
            dir);

        Debug.Log(
            $"[PlayerCombat] MAGIC CAST {target.name}");
    }
}