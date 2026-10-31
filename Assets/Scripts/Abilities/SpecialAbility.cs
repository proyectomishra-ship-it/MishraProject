using UnityEngine;

/// <summary>
/// Base de las habilidades especiales (click derecho) de cada clase.
///
/// Cada clase tiene su PROPIO asset con SOLO sus variables
/// (MageShockwaveAbility, WarriorBladeAbility, HunterBarrageAbility),
/// así que ningún valor se comparte entre clases. El asset se asigna en el
/// campo "Special Ability" del PlayerClassData de cada clase.
///
/// Lo único común es la recarga: cada asset tiene su propio cooldown.
/// </summary>
public abstract class SpecialAbility : ScriptableObject
{
    [Header("Recarga")]
    [Tooltip("Segundos hasta poder volver a usar esta habilidad.")]
    [SerializeField, Min(0f)] private float cooldown = 8f;

    public float Cooldown => cooldown;

    /// <summary>Maná que consume. Solo el Mage lo sobreescribe; el resto depende del cooldown.</summary>
    public virtual float ManaCost => 0f;

    /// <summary>
    /// Reproduce el efecto visual. Se ejecuta en TODOS los clientes
    /// (lo dispara un ClientRpc) y no afecta al gameplay.
    /// </summary>
    public abstract void PlayVfx(Player caster, Vector3 position, Vector3 direction);
}
