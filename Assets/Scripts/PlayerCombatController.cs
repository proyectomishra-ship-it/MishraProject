
using UnityEngine;
using Unity.Netcode;

public class PlayerCombatController : NetworkBehaviour
{
    private Player player;
    private CharacterStats stats;
    private EquipmentController equipment;
    private ResourceController resources;
    private TargetingController targeting;
    private PlayerAnimationController anim;

    [Header("Fallback")]
    [SerializeField] private float holdThreshold = 0.4f;
    [SerializeField] private float fallbackHeavyMultiplier = 2f;
    [SerializeField] private float fallbackHeavyStaminaCost = 25f;
    [SerializeField] private float fallbackMagicManaCost = 20f;

    [Header("Melee Hit Validation")]
    [SerializeField] private float attackConeAngle = 60f;
    [SerializeField] private float sphereCastRadius = 0.8f;
    [SerializeField] private LayerMask enemyLayer;

    private float holdTimer;
    private bool isHoldingAttack;
    private bool heavyTriggered;
    private float nextAttackTime;

    public void Initialize(Player player)
    {
        this.player = player;
        stats = player.GetStats();
        equipment = player.GetEquipment();
        resources = player.GetResourceController();
        targeting = player.GetComponent<TargetingController>();
        anim = player.GetComponent<PlayerAnimationController>();
    }

    private WeaponData GetCurrentWeapon()
        => equipment?.GetEquippedWeapon();

    private IWeaponBehavior GetCurrentBehavior()
        => WeaponBehaviorFactory.Create(GetCurrentWeapon(), transform);

    private float GetEffectiveHoldThreshold()
    {
        WeaponData weapon = GetCurrentWeapon();

        return weapon == null || weapon.AttackSpeed <= 0f
            ? holdThreshold
            : holdThreshold / weapon.AttackSpeed;
    }

    private float GetHeavyMultiplier()
    {
        WeaponData weapon = GetCurrentWeapon();
        return weapon != null
            ? weapon.HeavyMultiplier
            : fallbackHeavyMultiplier;
    }

    // =========================================================
    // AIM
    // =========================================================

    private Vector3 GetAimDirection()
    {
        Camera cam = Camera.main;

        if (cam == null)
            return player.transform.forward;

        Ray ray = cam.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        return ray.direction.normalized;
    }

    // =========================================================
    // INPUT
    // =========================================================

    public void OnAttackPressed()
    {
        if (!IsOwner || player == null || player.IsDead())
            return;

        isHoldingAttack = true;
        heavyTriggered = false;
        holdTimer = 0f;
    }

    public void OnAttackHeld()
    {
        if (!IsOwner || !isHoldingAttack || heavyTriggered)
            return;

        holdTimer += Time.deltaTime;

        if (holdTimer >= GetEffectiveHoldThreshold())
        {
            heavyTriggered = true;
            isHoldingAttack = false;

            RequestHeavyAttackServerRpc(GetAimDirection());
        }
    }

    public void OnAttackReleased()
    {
        if (!IsOwner || !isHoldingAttack)
            return;

        isHoldingAttack = false;

        if (!heavyTriggered)
            RequestAttackServerRpc(GetAimDirection());
    }

    // =========================================================
    // RPC
    // =========================================================

    [ServerRpc]
    private void RequestAttackServerRpc(Vector3 aimDirection)
    {
        PerformAttack(false, aimDirection);
    }

    [ServerRpc]
    private void RequestHeavyAttackServerRpc(Vector3 aimDirection)
    {
        PerformAttack(true, aimDirection);
    }

   
    // Método local llamado desde Player.cs.
    public void RequestSpecialAttack()
    {
        if (!IsOwner || player == null || player.IsDead())
            return;

        RequestSpecialAttackInternalServerRpc(GetAimDirection());
    }

    [ServerRpc]
    private void RequestSpecialAttackInternalServerRpc(
        Vector3 aimDirection)
    {
        PerformSpecialAttack(aimDirection);
    }

    // =========================================================
    // ATTACK
    // =========================================================

    private void PerformAttack(bool heavy, Vector3 aimDirection)
    {
        if (!CanAttackNow())
            return;

        aimDirection = NormalizeAim(aimDirection);

        WeaponData weapon = GetCurrentWeapon();
        bool isRanged = weapon != null && weapon.IsRanged;

        // El ataque comienza aunque no exista un enemigo.
        // El target solo es necesario para aplicar daño melee.
        Character target = isRanged
            ? null
            : FindMeleeTarget(aimDirection);

        if (heavy)
        {
            float cost = weapon != null
                ? weapon.StaminaCost
                : fallbackHeavyStaminaCost;

            if (resources == null || !resources.UseStamina(cost))
            {
                Debug.Log("[PlayerCombat] Not enough stamina");
                return;
            }
        }

        PlayAttackFeedback(
            heavy ? PlayerAttackAnim.Heavy : PlayerAttackAnim.Light);

        // Ranged: dispara incluso si target es null.
        if (isRanged)
        {
            IWeaponBehavior behavior = GetCurrentBehavior();

            if (behavior is IAimedWeaponBehavior aimedBehavior)
            {
                if (heavy)
                    aimedBehavior.PerformAimedHeavyAttack(
                        player, aimDirection, GetHeavyMultiplier());
                else
                    aimedBehavior.PerformAimedAttack(
                        player, aimDirection);
            }
            else
            {
                Debug.LogError(
                    "[PlayerCombat] El arma ranged no implementa " +
                    "IAimedWeaponBehavior.");
            }

            return;
        }

        // Melee: sin blanco válido, el golpe falla pero se ejecuta.
        if (target == null)
            return;

        IWeaponBehavior meleeBehavior = GetCurrentBehavior();

        if (heavy)
            meleeBehavior.PerformHeavyAttack(
                player, target, GetHeavyMultiplier());
        else
            meleeBehavior.PerformAttack(player, target);

        Debug.Log(
            $"[PlayerCombat] ATTACK {player.name} -> {target.name} " +
            $"| Heavy:{heavy}");
    }

    // =========================================================
    // SPECIAL ATTACK
    // =========================================================

    private void PerformSpecialAttack(Vector3 aimDirection)
    {
        if (!CanAttackNow())
            return;

        aimDirection = NormalizeAim(aimDirection);

        WeaponData weapon = GetCurrentWeapon();
        bool isRanged = weapon != null && weapon.IsRanged;

        Character target = isRanged
            ? null
            : FindMeleeTarget(aimDirection);

        // En melee no se consume maná si no hay un blanco válido.
        if (!isRanged && target == null)
        {
            PlayAttackFeedback(PlayerAttackAnim.Special);
            return;
        }

        float manaCost = weapon != null
            ? weapon.ManaCost
            : fallbackMagicManaCost;

        if (resources == null || !resources.UseMana(manaCost))
        {
            Debug.Log("[PlayerCombat] Not enough mana");
            return;
        }

        PlayAttackFeedback(PlayerAttackAnim.Special);

        IWeaponBehavior behavior = GetCurrentBehavior();

        if (isRanged)
        {
            if (behavior is IAimedWeaponBehavior aimedBehavior)
            {
                aimedBehavior.PerformAimedSpecialAttack(
                    player, aimDirection);
            }
            else
            {
                Debug.LogError(
                    "[PlayerCombat] El arma especial no implementa " +
                    "IAimedWeaponBehavior.");
            }

            return;
        }

        behavior.PerformSpecialAttack(player, target);
    }

    // =========================================================
    // MELEE TARGET RESOLUTION — SERVER
    // =========================================================

    private Character FindMeleeTarget(Vector3 aimDirection)
    {
        float range = stats.AttackRange.Value;

        if (range <= 0f)
            return null;

        Vector3 origin =
            player.transform.position + Vector3.up;

        Collider[] hits = Physics.OverlapSphere(
            player.transform.position,
            range,
            enemyLayer,
            QueryTriggerInteraction.Collide);

        Character bestTarget = null;
        float bestScore = float.MaxValue;

        foreach (Collider hit in hits)
        {
            Character candidate =
                hit.GetComponentInParent<Character>();

            if (candidate == null ||
                candidate == player ||
                candidate.GetStats() == null ||
                candidate.GetStats().CurrentHealth <= 0)
            {
                continue;
            }

            Vector3 targetPosition =
                candidate.transform.position + Vector3.up;

            Vector3 toTarget = targetPosition - origin;
            float distance = toTarget.magnitude;

            if (distance > range || distance <= 0.001f)
                continue;

            float angle = Vector3.Angle(
                aimDirection, toTarget / distance);

            if (angle > attackConeAngle)
                continue;

            // Comprobar que el golpe alcanza al candidato y no
            // atraviesa un obstáculo antes de llegar a él.
            if (!ValidateMeleeHit(candidate, origin, toTarget, distance))
                continue;

            float score = angle * 2f + distance;

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private bool ValidateMeleeHit(
        Character target,
        Vector3 origin,
        Vector3 toTarget,
        float distance)
    {
        if (!Physics.SphereCast(
            origin,
            sphereCastRadius,
            toTarget.normalized,
            out RaycastHit hit,
            distance + 0.1f,
            enemyLayer,
            QueryTriggerInteraction.Collide))
        {
            return false;
        }

        Character hitCharacter =
            hit.collider.GetComponentInParent<Character>();

        return hitCharacter == target;
    }

    private Vector3 NormalizeAim(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.001f)
            return player.transform.forward;

        return direction.normalized;
    }

    // =========================================================
    // ANIMATION / COOLDOWN
    // =========================================================

    private bool CanAttackNow()
    {
        return IsServer &&
               player != null &&
               !player.IsDead() &&
               Time.time >= nextAttackTime;
    }

    private void PlayAttackFeedback(PlayerAttackAnim kind)
    {
        WeaponData weapon = GetCurrentWeapon();

        float attackSpeed =
            weapon != null && weapon.AttackSpeed > 0f
                ? weapon.AttackSpeed
                : 1f;

        nextAttackTime = Time.time + 1f / attackSpeed;

        anim?.PlayAttack(kind);
    }

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
