using UnityEngine;

/// <summary>
/// Warrior: una espada de energía mágica barre un arco al frente,
/// dañando a todos los enemigos que alcanza.
/// El aura del arma equipada se tiñe del color de la energía.
/// No consume maná: se limita solo con el cooldown.
/// </summary>
[CreateAssetMenu(fileName = "WarriorBlade", menuName = "RPG/Abilities/Warrior Blade")]
public class WarriorBladeAbility : SpecialAbility
{
    [Header("Daño")]
    [Tooltip("Multiplica el stat Attack del jugador.")]
    [SerializeField, Min(0f)] private float damageMultiplier = 2.5f;

    [Tooltip("Segundos entre el click y el daño.")]
    [SerializeField, Min(0f)] private float hitDelay = 0.12f;

    [Header("Tamaño de la hoja")]
    [Tooltip("LARGO: hasta dónde llega la hoja, en metros.")]
    [SerializeField, Min(1f)] private float bladeLength = 10f;

    [Tooltip("ANCHO: grosor visual de la hoja, en metros. " +
             "Un valor menor produce una espada más estilizada.")]
    [SerializeField, Min(0.05f)] private float bladeWidth = 0.22f;

    [Tooltip("Apertura del arco que cubre el golpe, en grados.")]
    [SerializeField, Range(30f, 300f)] private float sweepAngle = 140f;

    [Header("Efecto visual")]
    [Tooltip("Color de la hoja de energía y del aura del arma.")]
    [SerializeField]
    private Color energyColor =
        new Color(1f, 0.38f, 0.12f, 1f);

    [Tooltip("Segundos que dura el barrido de la hoja.")]
    [SerializeField, Min(0.1f)] private float slashDuration = 0.30f;

    [Tooltip("Segundos que el arma queda envuelta en aura de color.")]
    [SerializeField, Min(0.1f)] private float auraDuration = 1f;

    [Header("Posición del efecto visual")]
    [Tooltip("Desplazamiento relativo al jugador. X = lateral, Y = altura, Z = profundidad.")]
    [SerializeField] private Vector3 visualOffset = new Vector3(0f, 0.8f, 0f);

    [Tooltip("Opcional: prefab propio de partículas (sin NetworkObject). " +
             "Vacío = se genera la hoja de energía por código.")]
    [SerializeField] private GameObject effectPrefab;

    public float DamageMultiplier => damageMultiplier;
    public float HitDelay => hitDelay;
    public float BladeLength => bladeLength;
    public float SweepAngle => sweepAngle;

    public override void PlayVfx(
        Player caster,
        Vector3 position,
        Vector3 direction)
    {
        SpecialAbilityVfx.PlayWeaponAura(
            caster,
            energyColor,
            auraDuration);

        Vector3 worldOffset =
            caster.transform.right * visualOffset.x +
            Vector3.up * visualOffset.y +
            caster.transform.forward * visualOffset.z;

        if (effectPrefab != null)
        {
            SpecialAbilityVfx.PlayPrefab(
                effectPrefab,
                position + worldOffset,
                direction);

            return;
        }

        BladeSlashAnimator.Spawn(
            caster.transform,
            visualOffset,
            direction,
            bladeLength,
            bladeWidth,
            sweepAngle,
            slashDuration,
            energyColor);
    }
}