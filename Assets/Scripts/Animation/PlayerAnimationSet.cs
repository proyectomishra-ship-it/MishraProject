using System;
using System.Collections.Generic;
using UnityEngine;

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
    public CharacterClipSet clips = new CharacterClipSet();
}

/// <summary>
/// Animation Set del jugador: además de los clips base permite variantes por arma.
/// Los campos serializados (baseClips, weaponOverrides, controller) conservan sus
/// nombres, así que los assets existentes (ej: ZitaAnimationSet) siguen cargando igual.
/// </summary>
[CreateAssetMenu(menuName = "Mishra/Animation/Player Animation Set", fileName = "PlayerAnimationSet")]
public class PlayerAnimationSet : CharacterAnimationSet
{
    [Header("Variantes por tipo de arma (opcional)")]
    public List<WeaponAnimationOverride> weaponOverrides = new List<WeaponAnimationOverride>();

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
