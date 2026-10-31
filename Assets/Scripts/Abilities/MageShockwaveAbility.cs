using UnityEngine;

/// <summary>
/// Mage: onda expansiva centrada en el jugador que daña a todos los
/// enemigos a su alrededor. Es la única habilidad que consume maná.
/// </summary>
[CreateAssetMenu(fileName = "MageShockwave", menuName = "RPG/Abilities/Mage Shockwave")]
public class MageShockwaveAbility : SpecialAbility
{
    [Header("Daño y área")]
    [Tooltip("Multiplica el stat Attack del jugador.")]
    [SerializeField, Min(0f)] private float damageMultiplier = 2f;

    [Tooltip("Radio de la onda, en metros.")]
    [SerializeField, Min(0.5f)] private float radius = 8f;

    [Header("Costo")]
    [Tooltip("Maná que consume cada vez que se lanza.")]
    [SerializeField, Min(0f)] private float manaCost = 25f;

    [Header("Efecto visual")]
    [SerializeField] private Color waveColor = new Color(0.45f, 0.72f, 1f, 1f);

    [Tooltip("Opcional: prefab propio de partículas (sin NetworkObject). " +
             "Vacío = se genera el efecto por código.")]
    [SerializeField] private GameObject effectPrefab;

    public float DamageMultiplier => damageMultiplier;
    public float Radius => radius;
    public override float ManaCost => manaCost;

    public override void PlayVfx(Player caster, Vector3 position, Vector3 direction)
    {
        if (effectPrefab != null)
            SpecialAbilityVfx.PlayPrefab(effectPrefab, position, direction);
        else
            SpecialAbilityVfx.PlayShockwave(position, radius, waveColor);
    }
}
