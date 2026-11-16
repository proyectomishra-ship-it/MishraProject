using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum AttackAnimKind : byte
{
    Light   = 0,
    Heavy   = 1,
    Special = 2
}

/// <summary>
/// Base del sistema de animación de personajes (jugador y enemigos).
/// Corre en TODOS los clientes (propio, remotos y host).
///
/// Principio de diseño: el servidor sigue siendo la autoridad; este componente
/// solo reproduce lo que ya pasó.
///   • Locomoción / salto / caída → se DERIVAN localmente de la posición
///     sincronizada por NetworkTransform. Cero tráfico de red extra.
///   • Ataques y golpes recibidos → el servidor llama PlayAttack()/PlayHit()
///     y se propaga con un ClientRpc (son eventos puntuales).
///   • Muerte, arma, etc. → cada subclase decide de dónde la lee.
///
/// Subclases: PlayerAnimationController, EnemyAnimationController.
/// </summary>
[DisallowMultipleComponent]
public abstract class CharacterAnimationController : NetworkBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────
    [Header("Datos")]
    [SerializeField] protected CharacterAnimationSet animationSet;

    [Tooltip("Animator del MODELO (el hijo con el esqueleto). Vacío = se busca solo.")]
    [SerializeField] protected Animator animator;

    [Header("Locomoción")]
    [SerializeField] protected float speedDampTime = 0.08f;
    [Tooltip("Si en un frame se mueve más que esto, se asume teletransporte y se ignora.")]
    [SerializeField] protected float teleportDistance = 3f;
    [SerializeField] protected float minMoveSpeed = 0.05f;

    [Header("Reacción a golpes")]
    [SerializeField] protected float minTimeBetweenHits = 0.4f;

    // ── Estado ───────────────────────────────────────────────────────────────
    private AnimatorOverrideController overrideController;
    private readonly List<KeyValuePair<AnimationClip, AnimationClip>> overrideBuffer = new();

    private bool ready;
    private Vector3 lastPosition;
    private float smoothedVertical;
    private float currentAttackSpeed = 1f;
    private float lastHitTime = -999f;

    private static readonly AnimSlot[] AllSlots = (AnimSlot[])Enum.GetValues(typeof(AnimSlot));

    // ── API pública ──────────────────────────────────────────────────────────

    /// <summary>True mientras el personaje está en un ataque (tag "Action").</summary>
    public bool IsPlayingAction =>
        ready && animator.GetCurrentAnimatorStateInfo(0).tagHash == CharacterAnimatorParams.TagActionId;

    /// <summary>Solo servidor. Reproduce el ataque en todos los clientes.</summary>
    public void PlayAttack(AttackAnimKind kind)
    {
        if (!IsServer) return;
        PlayAttackClientRpc((byte)kind);
    }

    /// <summary>Solo servidor. Reproduce la reacción de golpe en todos los clientes.</summary>
    public void PlayHit()
    {
        if (!IsServer || IsDeadNow()) return;
        PlayHitClientRpc();
    }

    // ── Ganchos para las subclases ───────────────────────────────────────────

    /// <summary>True una vez que el Animator quedó listo (después de OnNetworkSpawn).</summary>
    protected bool Ready => ready;

    /// <summary>Qué hace el Animator cuando el personaje no se ve.</summary>
    protected virtual AnimatorCullingMode CullingMode => AnimatorCullingMode.AlwaysAnimate;

    /// <summary>Convierte m/s reales a la escala del blend tree (0 quieto, 1 caminar, 2 correr).</summary>
    protected abstract float MapSpeed(float metersPerSecond);

    /// <summary>Por defecto siempre apoyado (los enemigos van por NavMesh). El jugador lo sobreescribe.</summary>
    protected virtual bool ComputeGrounded(float dt) => true;

    /// <summary>True si el personaje está muerto. Lo leen los RPC para no animar a un cadáver.</summary>
    protected virtual bool IsDeadNow() => false;

    /// <summary>El Animator ya está configurado: acá se suscriben eventos, etc.</summary>
    protected virtual void OnAnimatorReady() { }

    /// <summary>Antes de destruir el override controller: acá se desuscribe todo.</summary>
    protected virtual void OnAnimatorTeardown() { }

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    protected virtual void Awake()
    {
        if (animator == null)
            animator = FindModelAnimator();
    }

    public override void OnNetworkSpawn()
    {
        // Servidor dedicado: no hay pantalla, no hay nada que animar. El hit y la
        // muerte los decide el servidor sin mirar el Animator. (El host SÍ anima.)
        if (IsServer && !IsClient)
        {
            enabled = false;
            return;
        }

        if (!SetupAnimator())
        {
            enabled = false;
            return;
        }

        lastPosition = transform.position;

        OnAnimatorReady();
    }

    public override void OnNetworkDespawn()
    {
        if (ready)
            OnAnimatorTeardown();

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
            Debug.LogError($"[CharacterAnimation] {name}: falta 'Animation Set', o el set no tiene " +
                           "controller generado (botón 'Generar Animator Controller').", this);
            return false;
        }

        if (animator == null)
            animator = FindModelAnimator();

        if (animator == null)
        {
            animator = GetComponent<Animator>();

            if (animator == null)
            {
                Debug.LogError($"[CharacterAnimation] {name}: no hay ningún Animator. " +
                               "Agregá un Animator (sin controller) al hijo que tiene el esqueleto.", this);
                return false;
            }

            Debug.LogWarning($"[CharacterAnimation] {name}: usando el Animator de la RAÍZ. Si el esqueleto " +
                             "es un hijo, los clips no van a encontrar los huesos. " +
                             "Poné el Animator en el modelo.", this);
        }

        overrideController = new AnimatorOverrideController(animationSet.controller)
        {
            name = animationSet.controller.name + "_runtime"
        };

        animator.runtimeAnimatorController = overrideController;
        animator.applyRootMotion = false; // el movimiento lo manda el NetworkTransform / NavMeshAgent
        animator.cullingMode = CullingMode;

        animator.SetFloat(CharacterAnimatorParams.AttackSpeedId, 1f);

        ready = true;
        return true;
    }

    // =========================================================================
    // LOCOMOCIÓN — corre en todos los clientes
    // =========================================================================

    protected virtual void Update()
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

        bool grounded = ComputeGrounded(dt);

        smoothedVertical = Mathf.Lerp(smoothedVertical, verticalSpeed, 1f - Mathf.Exp(-20f * dt));

        animator.SetFloat(CharacterAnimatorParams.SpeedId, MapSpeed(horizontalSpeed), speedDampTime, dt);
        animator.SetBool(CharacterAnimatorParams.GroundedId, grounded);
        animator.SetFloat(CharacterAnimatorParams.VerticalVelocityId, grounded ? 0f : smoothedVertical);
    }

    /// <summary>
    /// Mapea m/s reales a la escala del blend tree dados los m/s de "caminar" y de "correr":
    /// 0 = quieto, 1 = velocidad de caminar, 2 = velocidad de correr (con clamp).
    /// </summary>
    protected static float MapToBlend(float metersPerSecond, float walkSpeed, float runSpeed)
    {
        if (walkSpeed <= 0.01f) return 0f;

        float run = Mathf.Max(runSpeed, walkSpeed * 1.01f);

        if (metersPerSecond <= walkSpeed)
            return metersPerSecond / walkSpeed;

        return 1f + Mathf.Clamp01((metersPerSecond - walkSpeed) / (run - walkSpeed));
    }

    // =========================================================================
    // COMBATE — el servidor llama, todos reproducen
    // =========================================================================

    [ClientRpc]
    private void PlayAttackClientRpc(byte kind)
    {
        if (!ready || IsDeadNow()) return;

        if (!TryResolveAttack((AttackAnimKind)kind, out AttackAnimKind resolved))
            return; // el set no tiene ningún clip de ataque

        animator.SetFloat(CharacterAnimatorParams.AttackSpeedId, currentAttackSpeed);

        // Si un Hit quedó pendiente justo cuando arrancó el ataque, el trigger sobrevive
        // y el personaje hacía el "flinch" recién al terminar el ataque. Se descarta.
        animator.ResetTrigger(CharacterAnimatorParams.HitId);

        switch (resolved)
        {
            case AttackAnimKind.Light:
                animator.SetTrigger(CharacterAnimatorParams.AttackLightId);
                break;
            case AttackAnimKind.Heavy:
                animator.SetTrigger(CharacterAnimatorParams.AttackHeavyId);
                break;
            case AttackAnimKind.Special:
                animator.SetTrigger(CharacterAnimatorParams.AttackSpecialId);
                break;
        }
    }

    [ClientRpc]
    private void PlayHitClientRpc()
    {
        if (!ready || IsDeadNow()) return;

        // Sin clip de Hit no existe el estado que consume el trigger.
        if (!animationSet.HasClip(AnimSlot.Hit)) return;

        if (Time.time - lastHitTime < minTimeBetweenHits) return;

        // Solo reaccionamos si está en locomoción "limpia": no durante un ataque
        // ni a mitad de una transición. Evita triggers viejos que se disparan tarde.
        if (animator.IsInTransition(0)) return;
        if (animator.GetCurrentAnimatorStateInfo(0).tagHash != CharacterAnimatorParams.TagLocomotionId) return;

        lastHitTime = Time.time;
        animator.SetTrigger(CharacterAnimatorParams.HitId);
    }

    /// <summary>
    /// Si el set no tiene el clip pedido usa el más parecido: Special → Heavy → Light.
    /// Devuelve false si no hay ningún clip de ataque.
    /// </summary>
    private bool TryResolveAttack(AttackAnimKind requested, out AttackAnimKind resolved)
    {
        resolved = requested;
        if (HasAttackClip(resolved)) return true;

        if (resolved == AttackAnimKind.Special)
        {
            resolved = AttackAnimKind.Heavy;
            if (HasAttackClip(resolved)) return true;
        }

        if (resolved == AttackAnimKind.Heavy)
        {
            resolved = AttackAnimKind.Light;
            if (HasAttackClip(resolved)) return true;
        }

        return false;
    }

    private bool HasAttackClip(AttackAnimKind kind)
    {
        switch (kind)
        {
            case AttackAnimKind.Heavy:   return animationSet.HasClip(AnimSlot.AttackHeavy);
            case AttackAnimKind.Special: return animationSet.HasClip(AnimSlot.AttackSpecial);
            default:                     return animationSet.HasClip(AnimSlot.AttackLight);
        }
    }

    /// <summary>Velocidad de las animaciones de ataque (ej: la del arma equipada). 1 = normal.</summary>
    protected void SetAttackSpeed(float value)
    {
        currentAttackSpeed = value;

        if (ready)
            animator.SetFloat(CharacterAnimatorParams.AttackSpeedId, currentAttackSpeed);
    }

    // =========================================================================
    // MUERTE
    // =========================================================================

    /// <summary>Marca/desmarca IsDead y descarta triggers pendientes.</summary>
    protected void ApplyDeadState(bool dead)
    {
        if (!ready) return;

        animator.ResetTrigger(CharacterAnimatorParams.AttackLightId);
        animator.ResetTrigger(CharacterAnimatorParams.AttackHeavyId);
        animator.ResetTrigger(CharacterAnimatorParams.AttackSpecialId);
        animator.ResetTrigger(CharacterAnimatorParams.HitId);

        animator.SetBool(CharacterAnimatorParams.IsDeadId, dead);
    }

    /// <summary>Cliente que entra con el personaje ya muerto: directo a la pose final.</summary>
    protected void SnapToDeathPose()
    {
        if (!ready) return;

        animator.SetBool(CharacterAnimatorParams.IsDeadId, true);

        if (animator.HasState(0, CharacterAnimatorParams.StateDeathId))
            animator.Play(CharacterAnimatorParams.StateDeathId, 0, 1f);
    }

    // =========================================================================
    // VARIANTES DE CLIPS (ej: por arma equipada)
    // =========================================================================

    /// <summary>
    /// Para cada clip del controller base busca a qué "hueco" corresponde y lo
    /// reemplaza por el de 'overrides' (si lo define). null = vuelve a los clips base.
    /// </summary>
    protected void ApplyClipOverrides(CharacterClipSet overrides)
    {
        if (!ready) return;

        overrideBuffer.Clear();
        overrideController.GetOverrides(overrideBuffer);

        CharacterClipSet baseClips = animationSet.baseClips;

        for (int i = 0; i < overrideBuffer.Count; i++)
        {
            AnimationClip original = overrideBuffer[i].Key;
            AnimationClip replacement = null;

            if (overrides != null)
            {
                foreach (AnimSlot slot in AllSlots)
                {
                    if (baseClips.Get(slot) == original)
                    {
                        replacement = overrides.Get(slot); // null = se queda el original
                        break;
                    }
                }
            }

            overrideBuffer[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
        }

        overrideController.ApplyOverrides(overrideBuffer);
    }
}
