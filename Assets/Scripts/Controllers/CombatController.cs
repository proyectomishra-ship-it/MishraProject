using UnityEngine;
using Unity.Netcode;


public class CombatController : NetworkBehaviour
{
    private Character character;
    private CharacterStats stats;
    private EquipmentController equipmentController;
    private CharacterAnimationController animationController;

    [Header("Fallback — sin arma equipada")]
    [SerializeField] private float heavyAttackMultiplier = 2f;
    [SerializeField] private float holdThreshold = 0.4f;

    [Header("Validación de hit")]
    [SerializeField] private float attackConeAngle = 60f;
    [SerializeField] private float sphereCastRadius = 0.8f;
    [SerializeField] private LayerMask enemyLayer;


    private float attackHeldTime = 0f;
    private bool isHoldingAttack = false;
    private bool heavyConsumed = false;

    // =========================
    // INIT
    // =========================

    public void Initialize(Character character)
    {
        this.character = character;
        this.stats = character.GetStats();
        equipmentController = character.GetEquipment();

        // Opcional: personajes sin animacion (ej: NPCs) simplemente no animan el ataque.
        var anim = character.GetComponent<CharacterAnimationController>();
        animationController = anim != null ? anim : null;
    }

    // =========================
    // HELPERS — arma actual
    // =========================

    private IWeaponBehavior GetCurrentBehavior()
    {
        var weapon = equipmentController?.GetEquippedWeapon();
        return WeaponBehaviorFactory.Create(weapon, transform);
    }

    private float GetEffectiveHoldThreshold()
    {
        var weapon = equipmentController?.GetEquippedWeapon();
        if (weapon == null || weapon.AttackSpeed <= 0f) return holdThreshold;
        return holdThreshold / weapon.AttackSpeed;
    }

    private float GetHeavyMultiplier()
    {
        var weapon = equipmentController?.GetEquippedWeapon();
        return weapon != null ? weapon.HeavyMultiplier : heavyAttackMultiplier;
    }

    // =========================
    // API DE INPUT
    // =========================

    public void OnAttackPressed()
    {
        if (!IsServer) return;

        isHoldingAttack = true;
        attackHeldTime = 0f;
        heavyConsumed = false;
    }

    public void OnAttackHeld(float deltaTime)
    {
        if (!IsServer || !isHoldingAttack || heavyConsumed) return;

        attackHeldTime += deltaTime;

        if (attackHeldTime >= GetEffectiveHoldThreshold())
        {
            PerformAttack(heavy: true);
            heavyConsumed = true;
            isHoldingAttack = false;
        }
    }

    public void OnAttackReleased()
    {
        if (!IsServer || !isHoldingAttack) return;

        isHoldingAttack = false;

        if (!heavyConsumed)
            PerformAttack(heavy: false);
    }


    public void AttackDirect(bool heavy = false)
    {
        if (!IsServer) return;
        PlayAttackAnimation(heavy ? AttackAnimKind.Heavy : AttackAnimKind.Light);
        PerformAttack(heavy);
    }

    public void SpecialAttackDirect()
    {
        if (!IsServer) return;
        PlayAttackAnimation(AttackAnimKind.Special);
        PerformSpecialAttack();
    }

    // La animacion se dispara al decidir el ataque, antes de validar el golpe: si el
    // golpe no conecta igual se ve el swing. El ataque ligero de la IA no pasa por
    // aca sino por Enemy.OnAttackPressed (tambien lo usan los enemigos a distancia).
    private void PlayAttackAnimation(AttackAnimKind kind)
    {
        if (animationController != null)
            animationController.PlayAttack(kind);
    }

    // =========================
    // CORE COMBAT — servidor
    // =========================

    private void PerformAttack(bool heavy)
    {
        Character target = FindTarget();
        if (target == null)
        {
            Debug.LogWarning($"[Combat] {character.name}: sin target válido");
            return;
        }

        if (!ValidateHitWithSphereCast(target))
        {
            Debug.LogWarning($"[Combat] {character.name}: SphereCast no alcanzó a {target.name}");
            return;
        }

        var behavior = GetCurrentBehavior();

        // Audio: avisa a todos los clientes para que suene el ataque (si el prefab tiene CharacterAudio).
        GetComponent<CharacterAudio>()?.PlayAttackOnAllClients(heavy ? AttackSoundKind.Heavy : AttackSoundKind.Light);

        if (heavy)
            behavior.PerformHeavyAttack(character, target, GetHeavyMultiplier());
        else
            behavior.PerformAttack(character, target);

        Debug.Log($"[Combat] {character.name} → {target.name} (heavy:{heavy})");
    }

    private void PerformSpecialAttack()
    {
        Character target = FindTarget();
        if (target == null)
        {
            Debug.LogWarning($"[Combat] {character.name}: SpecialAttack sin target");
            return;
        }

        if (!ValidateHitWithSphereCast(target))
        {
            Debug.LogWarning($"[Combat] {character.name}: SpecialAttack spherecast falló");
            return;
        }

        // Audio: avisa a todos los clientes para que suene el ataque especial.
        GetComponent<CharacterAudio>()?.PlayAttackOnAllClients(AttackSoundKind.Special);

        GetCurrentBehavior().PerformSpecialAttack(character, target);
        Debug.Log($"[Combat] SPECIAL {character.name} → {target.name}");
    }

    // =========================
    // TARGETING
    // =========================

    private Character FindTarget()
    {
        Character target = character.GetComponent<TargetingController>()?.CurrentTarget;

        if (target == null)
            target = CombatTargetingSystem.FindBestTarget(
                character,
                stats.AttackRange.Value,
                attackConeAngle,
                enemyLayer);

        return target;
    }

    private bool ValidateHitWithSphereCast(Character target)
    {
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        Vector3 targetPos = target.transform.position + Vector3.up * 1.0f;
        Vector3 dir = (targetPos - origin).normalized;
        float distance = Vector3.Distance(origin, targetPos);

        float castRange = distance + 0.5f;

        Debug.Log($"[Combat] SphereCast: dist={distance:F2} castRange={castRange:F2}");

        if (Physics.SphereCast(
                origin,
                sphereCastRadius,
                dir,
                out RaycastHit hit,
                castRange,
                enemyLayer,
                QueryTriggerInteraction.Collide))
        {
            Character hitChar = hit.collider.GetComponentInParent<Character>();
            Debug.Log($"[Combat] SphereCast hit: {hit.collider.name}");
            return hitChar != null && hitChar == target;
        }

        Debug.DrawLine(origin, origin + dir * castRange, Color.red, 1f);
        return false;
    }

    // =========================
    // DEBUG
    // =========================

    private void OnDrawGizmosSelected()
    {
        if (stats == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stats.AttackRange.Value);
    }

    // =========================
    // PUBLIC API — acceso externo limpio
    // =========================


    public void Attack() => AttackDirect(heavy: false);
    public void SpecialAttack() => SpecialAttackDirect();
}
