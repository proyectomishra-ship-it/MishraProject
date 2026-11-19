using System;
using UnityEngine;

/// <summary>Los "huecos" de animación que entiende el sistema (jugador y enemigos).</summary>
public enum AnimSlot
{
    Idle, Walk, Run,
    Jump, Fall, Land,
    AttackLight, AttackHeavy, AttackSpecial,
    Hit, Death, Revive
}

/// <summary>Un juego completo de clips. Los campos vacíos son "no usar".</summary>
[Serializable]
public class CharacterClipSet
{
    [Header("Locomoción (idle y walk obligatorios en el set base)")]
    public AnimationClip idle;
    public AnimationClip walk;
    [Tooltip("Opcional. Sin 'run' el personaje usa 'walk' a toda velocidad.")]
    public AnimationClip run;

    [Header("Aire (solo jugador; los enemigos van por NavMesh y no lo usan)")]
    [Tooltip("Subida del salto. Si no hay 'fall', este clip se usa durante todo el aire.")]
    public AnimationClip jump;
    [Tooltip("Caída (loop).")]
    public AnimationClip fall;
    [Tooltip("Opcional. Aterrizaje corto.")]
    public AnimationClip land;

    [Header("Combate")]
    public AnimationClip attackLight;
    [Tooltip("Opcional. Sin este clip se usa el de 'attackLight'.")]
    public AnimationClip attackHeavy;
    [Tooltip("Opcional. Sin este clip se usa 'attackHeavy', o en su defecto 'attackLight'.")]
    public AnimationClip attackSpecial;

    [Header("Momento del impacto (solo jugador)")]
    [Tooltip("Segundos, contados desde el inicio del clip a velocidad 1, en que el golpe conecta o se suelta " +
             "la flecha / el hechizo. El daño o el proyectil se aplica en ese instante para quedar sincronizado " +
             "con la animacion. Se divide solo por la velocidad de ataque del arma. 0 = al instante.")]
    [Min(0f)] public float attackLightHitTime;
    [Min(0f)] public float attackHeavyHitTime;
    [Min(0f)] public float attackSpecialHitTime;

    [Header("Reacciones")]
    [Tooltip("Opcional. Sin este clip no hay reacción al recibir un golpe.")]
    public AnimationClip hit;
    [Tooltip("Sin Loop Time: debe quedarse en la última pose. " +
             "En enemigos es opcional: sin este clip desaparecen al instante, como antes.")]
    public AnimationClip death;
    [Tooltip("Opcional. Levantarse tras el respawn (solo jugador).")]
    public AnimationClip revive;

    /// <summary>Segundos (a velocidad 1) hasta el impacto del ataque de ese hueco. 0 si no aplica.</summary>
    public float GetHitTime(AnimSlot slot)
    {
        float time;

        switch (slot)
        {
            case AnimSlot.AttackLight:   return attackLightHitTime;
            case AnimSlot.AttackHeavy:   time = attackHeavyHitTime;   break;
            case AnimSlot.AttackSpecial: time = attackSpecialHitTime; break;
            default:                     return 0f;
        }

        // Si este ataque reutiliza el clip del ligero (mismo clip, por presupuesto) y no
        // tiene tiempo propio, hereda el del ligero: asi alcanza con configurar uno solo.
        if (time <= 0f && attackLight != null && Get(slot) == attackLight)
            return attackLightHitTime;

        return time;
    }

    public AnimationClip Get(AnimSlot slot)
    {
        switch (slot)
        {
            case AnimSlot.Idle:          return idle;
            case AnimSlot.Walk:          return walk;
            case AnimSlot.Run:           return run;
            case AnimSlot.Jump:          return jump;
            case AnimSlot.Fall:          return fall;
            case AnimSlot.Land:          return land;
            case AnimSlot.AttackLight:   return attackLight;
            case AnimSlot.AttackHeavy:   return attackHeavy;
            case AnimSlot.AttackSpecial: return attackSpecial;
            case AnimSlot.Hit:           return hit;
            case AnimSlot.Death:         return death;
            case AnimSlot.Revive:        return revive;
            default:                     return null;
        }
    }
}

/// <summary>
/// Base de todos los "Animation Set" (jugador y enemigos).
/// 1. Arrastrá acá tus clips.
/// 2. Botón "Generar Animator Controller" → crea el controller con toda la lógica.
/// 3. Asignalo en el controller de animación del prefab
///    (PlayerAnimationController o EnemyAnimationController).
///
/// Es abstracta: se crean assets de PlayerAnimationSet o EnemyAnimationSet.
/// </summary>
public abstract class CharacterAnimationSet : ScriptableObject
{
    [Header("Clips base")]
    public CharacterClipSet baseClips = new CharacterClipSet();

    [Header("Generado automáticamente — no tocar")]
    [Tooltip("Lo asigna el botón 'Generar Animator Controller'.")]
    public RuntimeAnimatorController controller;

    /// <summary>True si el set base tiene un clip asignado en ese hueco.</summary>
    public bool HasClip(AnimSlot slot) => baseClips != null && baseClips.Get(slot) != null;
}