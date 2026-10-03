using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Los "huecos" de animación que entiende el sistema.</summary>
public enum PlayerAnimSlot
{
    Idle, Walk, Run,
    Jump, Fall, Land,
    AttackLight, AttackHeavy, AttackSpecial,
    Hit, Death, Revive
}

/// <summary>Un juego completo de clips. Los campos vacíos son "no usar".</summary>
[Serializable]
public class PlayerClipSet
{
    [Header("Locomoción (obligatorios en el set base)")]
    public AnimationClip idle;
    public AnimationClip walk;
    public AnimationClip run;

    [Header("Aire")]
    [Tooltip("Subida del salto. Si no hay 'fall', este clip se usa durante todo el aire.")]
    public AnimationClip jump;
    [Tooltip("Caída (loop).")]
    public AnimationClip fall;
    [Tooltip("Opcional. Aterrizaje corto.")]
    public AnimationClip land;

    [Header("Combate")]
    public AnimationClip attackLight;
    public AnimationClip attackHeavy;
    public AnimationClip attackSpecial;

    [Header("Reacciones")]
    public AnimationClip hit;
    [Tooltip("Sin Loop Time: debe quedarse en la última pose.")]
    public AnimationClip death;
    [Tooltip("Opcional. Levantarse tras el respawn.")]
    public AnimationClip revive;

    public AnimationClip Get(PlayerAnimSlot slot)
    {
        switch (slot)
        {
            case PlayerAnimSlot.Idle:          return idle;
            case PlayerAnimSlot.Walk:          return walk;
            case PlayerAnimSlot.Run:           return run;
            case PlayerAnimSlot.Jump:          return jump;
            case PlayerAnimSlot.Fall:          return fall;
            case PlayerAnimSlot.Land:          return land;
            case PlayerAnimSlot.AttackLight:   return attackLight;
            case PlayerAnimSlot.AttackHeavy:   return attackHeavy;
            case PlayerAnimSlot.AttackSpecial: return attackSpecial;
            case PlayerAnimSlot.Hit:           return hit;
            case PlayerAnimSlot.Death:         return death;
            case PlayerAnimSlot.Revive:        return revive;
            default:                           return null;
        }
    }
}

/// <summary>
/// Reemplazo de clips según el tipo de arma equipada
/// (ej: Bow → idle/walk/run/ataques de arquero).
/// Todo clip que quede vacío usa el clip del set base.
/// </summary>
[Serializable]
public class WeaponAnimationOverride
{
    public string label = "Nuevo override";
    public WeaponType[] weaponTypes;
    public PlayerClipSet clips = new PlayerClipSet();
}

/// <summary>
/// Asset central del sistema de animación del jugador.
/// 1. Arrastrá acá tus clips.
/// 2. Botón "Generar Animator Controller" → crea el controller con toda la lógica.
/// 3. Asignalo en PlayerAnimationController (prefab del jugador).
/// </summary>
[CreateAssetMenu(menuName = "Mishra/Animation/Player Animation Set", fileName = "PlayerAnimationSet")]
public class PlayerAnimationSet : ScriptableObject
{
    [Header("Clips base (armas sin override y fallback)")]
    public PlayerClipSet baseClips = new PlayerClipSet();

    [Header("Variantes por tipo de arma (opcional)")]
    public List<WeaponAnimationOverride> weaponOverrides = new List<WeaponAnimationOverride>();

    [Header("Generado automáticamente — no tocar")]
    [Tooltip("Lo asigna el botón 'Generar Animator Controller'.")]
    public RuntimeAnimatorController controller;

    public WeaponAnimationOverride FindOverride(WeaponType type)
    {
        if (weaponOverrides == null) return null;

        foreach (var o in weaponOverrides)
        {
            if (o?.weaponTypes == null) continue;
            if (Array.IndexOf(o.weaponTypes, type) >= 0) return o;
        }
        return null;
    }
}
