using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Animación del JUGADOR en todos los clientes. La lógica común (setup, locomoción,
/// ataques, golpes, muerte) vive en CharacterAnimationController; acá solo está lo
/// propio del jugador:
///   • Velocidad real → blend tree usando Stats.Speed y el RunMultiplier.
///   • Detección de suelo (salto / caída / aterrizaje).
///   • Muerte / respawn → se leen de PlayerRespawnController.IsDead.
///   • Arma equipada → se lee de EquipmentController.OnSlotChanged y cambia los clips.
/// </summary>
public class PlayerAnimationController : CharacterAnimationController
{
    // ── Inspector ────────────────────────────────────────────────────────────
    [Header("Detección de suelo")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [Tooltip("Tiempo en el aire antes de contar como 'no grounded' (evita parpadeo en escalones).")]
    [SerializeField] private float airborneDelay = 0.1f;

    // ── Referencias ──────────────────────────────────────────────────────────
    private Player player;
    private MovementController movement;
    private CharacterController characterController;
    private PlayerRespawnController respawn;
    private EquipmentController equipment;

    // ── Estado ───────────────────────────────────────────────────────────────
    private PlayerAnimationSet playerSet;
    private WeaponAnimationOverride appliedOverride;
    private bool weaponApplied;

    private bool grounded = true;
    private float airTime;

    private static readonly RaycastHit[] groundHits = new RaycastHit[8];

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    protected override void Awake()
    {
        base.Awake();

        player = GetComponent<Player>();
        movement = GetComponent<MovementController>();
        characterController = GetComponent<CharacterController>();
        respawn = GetComponent<PlayerRespawnController>();
        equipment = GetComponent<EquipmentController>();
    }

    protected override void OnAnimatorReady()
    {
        playerSet = animationSet as PlayerAnimationSet;

        if (respawn != null)
        {
            respawn.IsDead.OnValueChanged += HandleDeadChanged;

            // Cliente que entra con el jugador ya muerto: directo a la pose final.
            if (respawn.IsDead.Value)
                SnapToDeathPose();
        }

        if (equipment != null)
            equipment.OnSlotChanged += HandleSlotChanged;

        StartCoroutine(InitialWeaponRefresh());
    }

    protected override void OnAnimatorTeardown()
    {
        if (respawn != null)
            respawn.IsDead.OnValueChanged -= HandleDeadChanged;

        if (equipment != null)
            equipment.OnSlotChanged -= HandleSlotChanged;
    }

    // =========================================================================
    // LOCOMOCIÓN
    // =========================================================================

    /// <summary>
    /// Convierte m/s reales a la escala del blend tree:
    /// 0 = quieto, 1 = velocidad de caminar, 2 = velocidad de correr.
    /// Así el blend tree no depende de las stats ni del runMultiplier.
    /// </summary>
    protected override float MapSpeed(float metersPerSecond)
    {
        if (metersPerSecond < minMoveSpeed) return 0f;

        CharacterStats stats = player != null ? player.GetStats() : null;
        float walk = stats != null ? stats.Speed.Value : 0f;

        float runMultiplier = movement != null ? movement.RunMultiplier : 2f;
        float run = walk * Mathf.Max(runMultiplier, 1.01f);

        return MapToBlend(metersPerSecond, walk, run);
    }

    protected override bool ComputeGrounded(float dt)
    {
        if (IsOnGround())
        {
            airTime = 0f;
            grounded = true;
        }
        else
        {
            airTime += dt;
            if (airTime >= airborneDelay)
                grounded = false;
        }

        return grounded;
    }

    private bool IsOnGround()
    {
        float radius = 0.3f;
        Vector3 feet = transform.position;

        if (characterController != null)
        {
            radius = characterController.radius * 0.9f;
            feet = transform.TransformPoint(characterController.center)
                   + Vector3.down * (characterController.height * 0.5f);
        }

        Vector3 origin = feet + Vector3.up * (radius + 0.05f);
        float distance = 0.05f + groundCheckDistance;

        int count = Physics.SphereCastNonAlloc(
            origin, radius, Vector3.down, groundHits, distance,
            groundMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = groundHits[i].collider;
            if (col != null && !col.transform.IsChildOf(transform))
                return true;
        }

        return false;
    }

    // =========================================================================
    // MUERTE / RESPAWN
    // =========================================================================

    protected override bool IsDeadNow() => respawn != null && respawn.IsDead.Value;

    private void HandleDeadChanged(bool previous, bool current) => ApplyDeadState(current);

    // =========================================================================
    // ARMA EQUIPADA → variante de animaciones
    // =========================================================================

    private IEnumerator InitialWeaponRefresh()
    {
        // Mismo criterio que WeaponVisualController: esperar a que el equipo sincronice.
        yield return null;
        yield return null;

        if (equipment != null)
            ApplyWeapon(equipment.GetEquippedWeapon());
    }

    private void HandleSlotChanged(EquipmentSlot slot, IEquippable item)
    {
        if (slot != EquipmentSlot.Weapon) return;
        ApplyWeapon(item as WeaponData);
    }

    private void ApplyWeapon(WeaponData weapon)
    {
        if (!Ready) return;

        SetAttackSpeed(weapon != null && weapon.AttackSpeed > 0f ? weapon.AttackSpeed : 1f);

        WeaponType type = weapon != null ? weapon.WeaponType : WeaponType.None;
        WeaponAnimationOverride ov = playerSet != null ? playerSet.FindOverride(type) : null;

        if (weaponApplied && ov == appliedOverride) return;

        weaponApplied = true;
        appliedOverride = ov;

        ApplyClipOverrides(ov?.clips);
    }
}
