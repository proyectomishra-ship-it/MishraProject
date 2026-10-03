using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum PlayerAttackAnim : byte
{
    Light   = 0,
    Heavy   = 1,
    Special = 2
}

/// <summary>
/// Maneja el Animator del jugador en TODOS los clientes (propio, remotos y host).
///
/// Principio de diseño: el servidor sigue siendo la autoridad; este componente
/// solo reproduce lo que ya pasó.
///   • Locomoción / salto / caída → se DERIVAN localmente de la posición
///     sincronizada por NetworkTransform. Cero tráfico de red extra.
///   • Muerte / respawn → se leen de PlayerRespawnController.IsDead.
///   • Arma equipada → se lee de EquipmentController.OnSlotChanged.
///   • Ataques y golpes recibidos → el servidor llama PlayAttack()/PlayHit()
///     y se propaga con un ClientRpc (son eventos puntuales).
/// </summary>
[DisallowMultipleComponent]
public class PlayerAnimationController : NetworkBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────
    [Header("Datos")]
    [SerializeField] private PlayerAnimationSet animationSet;

    [Tooltip("Animator del MODELO (el hijo 'zita COMPLETO'). Vacío = se busca solo.")]
    [SerializeField] private Animator animator;

    [Header("Locomoción")]
    [SerializeField] private float speedDampTime = 0.08f;
    [Tooltip("Si en un frame se mueve más que esto, se asume teletransporte y se ignora.")]
    [SerializeField] private float teleportDistance = 3f;
    [SerializeField] private float minMoveSpeed = 0.05f;

    [Header("Detección de suelo")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [Tooltip("Tiempo en el aire antes de contar como 'no grounded' (evita parpadeo en escalones).")]
    [SerializeField] private float airborneDelay = 0.1f;

    [Header("Reacción a golpes")]
    [SerializeField] private float minTimeBetweenHits = 0.4f;

    // ── Referencias ──────────────────────────────────────────────────────────
    private Player player;
    private MovementController movement;
    private CharacterController characterController;
    private PlayerRespawnController respawn;
    private EquipmentController equipment;

    // ── Estado ───────────────────────────────────────────────────────────────
    private AnimatorOverrideController overrideController;
    private readonly List<KeyValuePair<AnimationClip, AnimationClip>> overrideBuffer = new();
    private WeaponAnimationOverride appliedOverride;
    private bool weaponApplied;

    private bool ready;
    private Vector3 lastPosition;
    private float smoothedVertical;
    private bool grounded = true;
    private float airTime;
    private float currentAttackSpeed = 1f;
    private float lastHitTime = -999f;

    private static readonly RaycastHit[] groundHits = new RaycastHit[8];

    // ── API pública ──────────────────────────────────────────────────────────

    /// <summary>True mientras el personaje está en un ataque (tag "Action").</summary>
    public bool IsPlayingAction =>
        ready && animator.GetCurrentAnimatorStateInfo(0).tagHash == PlayerAnimatorParams.TagActionId;

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        player = GetComponent<Player>();
        movement = GetComponent<MovementController>();
        characterController = GetComponent<CharacterController>();
        respawn = GetComponent<PlayerRespawnController>();
        equipment = GetComponent<EquipmentController>();

        if (animator == null)
            animator = FindModelAnimator();
    }

    public override void OnNetworkSpawn()
    {
        if (!SetupAnimator())
        {
            enabled = false;
            return;
        }

        lastPosition = transform.position;

        if (respawn != null)
        {
            respawn.IsDead.OnValueChanged += HandleDeadChanged;

            // Cliente que entra con el jugador ya muerto: directo a la pose final.
            if (respawn.IsDead.Value)
            {
                animator.SetBool(PlayerAnimatorParams.IsDeadId, true);
                if (animator.HasState(0, PlayerAnimatorParams.StateDeathId))
                    animator.Play(PlayerAnimatorParams.StateDeathId, 0, 1f);
            }
        }

        if (equipment != null)
            equipment.OnSlotChanged += HandleSlotChanged;

        StartCoroutine(InitialWeaponRefresh());
    }

    public override void OnNetworkDespawn()
    {
        if (respawn != null)
            respawn.IsDead.OnValueChanged -= HandleDeadChanged;

        if (equipment != null)
            equipment.OnSlotChanged -= HandleSlotChanged;

        if (overrideController != null)
        {
            Destroy(overrideController);
            overrideController = null;
        }

        ready = false;
    }

    // =========================================================================
    // SETUP
    // =========================================================================

    /// <summary>
    /// El Animator debe estar en el objeto que contiene el esqueleto (el modelo),
    /// porque los clips guardan rutas de huesos RELATIVAS a ese objeto. Un Animator
    /// en la raíz del prefab no encuentra los huesos y no anima nada.
    /// </summary>
    private Animator FindModelAnimator()
    {
        foreach (Animator a in GetComponentsInChildren<Animator>(true))
            if (a.gameObject != gameObject)
                return a;

        return null;
    }

    private bool SetupAnimator()
    {
        if (animationSet == null || animationSet.controller == null)
        {
            Debug.LogError($"[PlayerAnimation] {name}: falta 'Animation Set', o el set no tiene " +
                           "controller generado (botón 'Generar Animator Controller').");
            return false;
        }

        if (animator == null)
            animator = FindModelAnimator();

        if (animator == null)
        {
            animator = GetComponent<Animator>();

            if (animator == null)
            {
                Debug.LogError($"[PlayerAnimation] {name}: no hay ningún Animator.");
                return false;
            }

            Debug.LogWarning($"[PlayerAnimation] {name}: usando el Animator de la RAÍZ. Si el esqueleto " +
                             "es un hijo (zita COMPLETO), los clips no van a encontrar los huesos. " +
                             "Poné el Animator en el modelo.");
        }

        overrideController = new AnimatorOverrideController(animationSet.controller)
        {
            name = animationSet.controller.name + "_runtime"
        };

        animator.runtimeAnimatorController = overrideController;
        animator.applyRootMotion = false; // el movimiento lo manda MovementController / NetworkTransform
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        animator.SetFloat(PlayerAnimatorParams.AttackSpeedId, 1f);

        ready = true;
        return true;
    }

    // =========================================================================
    // LOCOMOCIÓN — corre en todos los clientes
    // =========================================================================

    private void Update()
    {
        if (!ready) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 pos = transform.position;
        Vector3 delta = pos - lastPosition;
        lastPosition = pos;

        if (delta.sqrMagnitude > teleportDistance * teleportDistance)
            delta = Vector3.zero;

        float horizontalSpeed = new Vector2(delta.x, delta.z).magnitude / dt;
        float verticalSpeed = delta.y / dt;

        UpdateGrounded(dt);

        smoothedVertical = Mathf.Lerp(smoothedVertical, verticalSpeed, 1f - Mathf.Exp(-20f * dt));

        animator.SetFloat(PlayerAnimatorParams.SpeedId, MapSpeed(horizontalSpeed), speedDampTime, dt);
        animator.SetBool(PlayerAnimatorParams.GroundedId, grounded);
        animator.SetFloat(PlayerAnimatorParams.VerticalVelocityId, grounded ? 0f : smoothedVertical);
    }

    /// <summary>
    /// Convierte m/s reales a la escala del blend tree:
    /// 0 = quieto, 1 = velocidad de caminar, 2 = velocidad de correr.
    /// Así el blend tree no depende de las stats ni del runMultiplier.
    /// </summary>
    private float MapSpeed(float metersPerSecond)
    {
        if (metersPerSecond < minMoveSpeed) return 0f;

        CharacterStats stats = player != null ? player.GetStats() : null;
        float walk = stats != null ? stats.Speed.Value : 0f;
        if (walk <= 0.01f) return 0f;

        float runMultiplier = movement != null ? movement.RunMultiplier : 2f;
        float run = walk * Mathf.Max(runMultiplier, 1.01f);

        if (metersPerSecond <= walk)
            return metersPerSecond / walk;

        return 1f + Mathf.Clamp01((metersPerSecond - walk) / (run - walk));
    }

    private void UpdateGrounded(float dt)
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
    // COMBATE — el servidor llama, todos reproducen
    // =========================================================================

    /// <summary>Solo servidor. Reproduce el ataque en todos los clientes.</summary>
    public void PlayAttack(PlayerAttackAnim kind)
    {
        if (!IsServer) return;
        PlayAttackClientRpc((byte)kind);
    }

    /// <summary>Solo servidor. Reproduce la reacción de golpe en todos los clientes.</summary>
    public void PlayHit()
    {
        if (!IsServer) return;
        PlayHitClientRpc();
    }

    [ClientRpc]
    private void PlayAttackClientRpc(byte kind)
    {
        if (!ready || IsDeadNow()) return;

        animator.SetFloat(PlayerAnimatorParams.AttackSpeedId, currentAttackSpeed);

        switch ((PlayerAttackAnim)kind)
        {
            case PlayerAttackAnim.Light:
                animator.SetTrigger(PlayerAnimatorParams.AttackLightId);
                break;
            case PlayerAttackAnim.Heavy:
                animator.SetTrigger(PlayerAnimatorParams.AttackHeavyId);
                break;
            case PlayerAttackAnim.Special:
                animator.SetTrigger(PlayerAnimatorParams.AttackSpecialId);
                break;
        }
    }

    [ClientRpc]
    private void PlayHitClientRpc()
    {
        if (!ready || IsDeadNow()) return;
        if (Time.time - lastHitTime < minTimeBetweenHits) return;

        // Solo reaccionamos si está en locomoción "limpia": no durante un ataque
        // ni a mitad de una transición. Evita triggers viejos que se disparan tarde.
        if (animator.IsInTransition(0)) return;
        if (animator.GetCurrentAnimatorStateInfo(0).tagHash != PlayerAnimatorParams.TagLocomotionId) return;

        lastHitTime = Time.time;
        animator.SetTrigger(PlayerAnimatorParams.HitId);
    }

    // =========================================================================
    // MUERTE / RESPAWN
    // =========================================================================

    private bool IsDeadNow() => respawn != null && respawn.IsDead.Value;

    private void HandleDeadChanged(bool previous, bool current)
    {
        if (!ready) return;

        animator.ResetTrigger(PlayerAnimatorParams.AttackLightId);
        animator.ResetTrigger(PlayerAnimatorParams.AttackHeavyId);
        animator.ResetTrigger(PlayerAnimatorParams.AttackSpecialId);
        animator.ResetTrigger(PlayerAnimatorParams.HitId);

        animator.SetBool(PlayerAnimatorParams.IsDeadId, current);
    }

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
        if (!ready) return;

        currentAttackSpeed = weapon != null && weapon.AttackSpeed > 0f ? weapon.AttackSpeed : 1f;
        animator.SetFloat(PlayerAnimatorParams.AttackSpeedId, currentAttackSpeed);

        WeaponType type = weapon != null ? weapon.WeaponType : WeaponType.None;
        WeaponAnimationOverride ov = animationSet.FindOverride(type);

        if (weaponApplied && ov == appliedOverride) return;

        weaponApplied = true;
        appliedOverride = ov;

        ApplyOverrides(ov);
    }

    /// <summary>
    /// Para cada clip del controller base busca a qué "hueco" corresponde
    /// y lo reemplaza por el del override (si el override lo define).
    /// </summary>
    private void ApplyOverrides(WeaponAnimationOverride ov)
    {
        overrideBuffer.Clear();
        overrideController.GetOverrides(overrideBuffer);

        PlayerClipSet baseClips = animationSet.baseClips;

        for (int i = 0; i < overrideBuffer.Count; i++)
        {
            AnimationClip original = overrideBuffer[i].Key;
            AnimationClip replacement = null;

            if (ov?.clips != null)
            {
                foreach (PlayerAnimSlot slot in System.Enum.GetValues(typeof(PlayerAnimSlot)))
                {
                    if (baseClips.Get(slot) == original)
                    {
                        replacement = ov.clips.Get(slot); // null = se queda el original
                        break;
                    }
                }
            }

            overrideBuffer[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
        }

        overrideController.ApplyOverrides(overrideBuffer);
    }
}
