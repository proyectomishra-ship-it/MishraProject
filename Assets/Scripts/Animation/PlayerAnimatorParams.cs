using UnityEngine;

/// <summary>
/// Nombres y hashes de los parámetros / tags / estados del Animator del jugador.
/// Un único lugar para que el Builder (Editor) y PlayerAnimationController
/// nunca queden desincronizados por un typo.
/// </summary>
public static class PlayerAnimatorParams
{
    // ── Parámetros ───────────────────────────────────────────────────────────
    public const string Speed            = "Speed";            // float  0 = quieto, 1 = caminar, 2 = correr
    public const string Grounded         = "Grounded";         // bool
    public const string VerticalVelocity = "VerticalVelocity"; // float  (+ sube, - cae)
    public const string IsDead           = "IsDead";           // bool
    public const string AttackSpeed      = "AttackSpeed";      // float  multiplicador de velocidad de ataques
    public const string AttackLight      = "AttackLight";      // trigger
    public const string AttackHeavy      = "AttackHeavy";      // trigger
    public const string AttackSpecial    = "AttackSpecial";    // trigger
    public const string Hit              = "Hit";              // trigger

    // ── Tags de estado (se usan para saber "qué está haciendo" el personaje) ──
    public const string TagLocomotion = "Locomotion";
    public const string TagAction     = "Action";   // ataques: no se interrumpen con Hit

    // ── Nombres de estado ────────────────────────────────────────────────────
    public const string StateDeath = "Death";

    // ── Hashes ───────────────────────────────────────────────────────────────
    public static readonly int SpeedId            = Animator.StringToHash(Speed);
    public static readonly int GroundedId         = Animator.StringToHash(Grounded);
    public static readonly int VerticalVelocityId = Animator.StringToHash(VerticalVelocity);
    public static readonly int IsDeadId           = Animator.StringToHash(IsDead);
    public static readonly int AttackSpeedId      = Animator.StringToHash(AttackSpeed);
    public static readonly int AttackLightId      = Animator.StringToHash(AttackLight);
    public static readonly int AttackHeavyId      = Animator.StringToHash(AttackHeavy);
    public static readonly int AttackSpecialId    = Animator.StringToHash(AttackSpecial);
    public static readonly int HitId              = Animator.StringToHash(Hit);

    public static readonly int TagLocomotionId = Animator.StringToHash(TagLocomotion);
    public static readonly int TagActionId     = Animator.StringToHash(TagAction);
    public static readonly int StateDeathId    = Animator.StringToHash(StateDeath);
}
