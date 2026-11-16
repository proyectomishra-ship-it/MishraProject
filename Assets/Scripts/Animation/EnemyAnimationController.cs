using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Animación de un ENEMIGO en todos los clientes. La lógica común vive en
/// CharacterAnimationController; acá solo está lo propio de los enemigos:
///   • Velocidad real → blend tree. Los enemigos no tienen Stats de "correr", así que
///     la velocidad de referencia sale del NavMeshAgent (o de los campos de abajo).
///   • Muerte: el servidor llama BeginDeath() y recién despawnea cuando termina el clip.
///     Se sincroniza con un NetworkVariable (también la ve quien entra tarde).
///
/// Cómo conectarlo a un prefab de enemigo:
///   1. Crear un EnemyAnimationSet, arrastrar los clips y "Generar Animator Controller".
///   2. En el prefab: agregar un Animator (SIN controller) al hijo que tiene el esqueleto.
///   3. Agregar este componente a la raíz y asignar el EnemyAnimationSet.
/// Los disparos de ataque y golpe ya están conectados desde Enemy y CombatController.
/// </summary>
public class EnemyAnimationController : CharacterAnimationController
{
    // ── Inspector ────────────────────────────────────────────────────────────
    [Header("Velocidad → blend tree")]
    [Tooltip("m/s a los que se ve el clip 'run' completo. 0 = usa la velocidad del NavMeshAgent del prefab.")]
    [SerializeField] private float runSpeed = 0f;

    [Tooltip("m/s a los que se ve el clip 'walk' completo. 0 = Run Speed × Walk Ratio.")]
    [SerializeField] private float walkSpeed = 0f;

    [Range(0.1f, 0.9f)]
    [SerializeField] private float walkRatio = 0.5f;

    [Header("Muerte")]
    [Tooltip("Segundos que el cuerpo se queda quieto después de terminar el clip de muerte, " +
             "antes de desaparecer. Solo aplica si el set tiene clip de muerte.")]
    [SerializeField] private float lingerAfterDeath = 0.75f;

    // ── Estado ───────────────────────────────────────────────────────────────
    private readonly NetworkVariable<bool> isDead = new NetworkVariable<bool>(false);

    private NavMeshAgent agent;
    private float referenceRunSpeed;

    // Los enemigos son muchos: si no se ven no hace falta mover huesos, pero sí
    // mantener la máquina de estados al día para que el parámetro Speed siga bien.
    protected override AnimatorCullingMode CullingMode => AnimatorCullingMode.CullUpdateTransforms;

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    protected override void Awake()
    {
        base.Awake();
        agent = GetComponent<NavMeshAgent>();
    }

    protected override void OnAnimatorReady()
    {
        // Se cachea ahora y no en cada frame: el servidor cambia agent.speed en
        // algunos estados (huir, atacar) y host/clientes deben mapear igual.
        referenceRunSpeed = runSpeed > 0f
            ? runSpeed
            : (agent != null ? agent.speed : 0f);

        isDead.OnValueChanged += HandleDeadChanged;

        // Cliente que entra con el enemigo ya muerto: directo a la pose final.
        if (isDead.Value)
        {
            if (!IsServer) DisableColliders();
            SnapToDeathPose();
        }
    }

    protected override void OnAnimatorTeardown()
    {
        isDead.OnValueChanged -= HandleDeadChanged;
    }

    // =========================================================================
    // LOCOMOCIÓN
    // =========================================================================

    protected override float MapSpeed(float metersPerSecond)
    {
        if (metersPerSecond < minMoveSpeed) return 0f;

        float run = referenceRunSpeed;
        float walk = walkSpeed > 0f ? walkSpeed : run * walkRatio;

        return MapToBlend(metersPerSecond, walk, run);
    }

    // =========================================================================
    // MUERTE
    // =========================================================================

    protected override bool IsDeadNow() => isDead.Value;

    /// <summary>
    /// Solo servidor. Arranca la animación de muerte en todos los clientes y apaga los
    /// colliders (para que nadie lo siga targeteando).
    /// Devuelve cuántos segundos hay que esperar antes de despawnear.
    /// 0 = el set no tiene clip de muerte: despawnear ya, como antes.
    /// </summary>
    public float BeginDeath()
    {
        if (!IsServer) return 0f;
        if (animationSet == null || animationSet.controller == null) return 0f;

        AnimationClip death = animationSet.baseClips != null ? animationSet.baseClips.death : null;
        if (death == null) return 0f;

        isDead.Value = true;
        DisableColliders();

        return death.length + lingerAfterDeath;
    }

    private void HandleDeadChanged(bool previous, bool current)
    {
        ApplyDeadState(current);

        // En el host/servidor los colliders ya los apagó BeginDeath().
        if (current && !IsServer)
            DisableColliders();
    }

    private void DisableColliders()
    {
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
            col.enabled = false;
    }
}
