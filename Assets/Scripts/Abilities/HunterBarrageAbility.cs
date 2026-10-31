using UnityEngine;

/// <summary>
/// Hunter: lluvia de flechas disparadas una tras otra, en ráfagas, con
/// daño aumentado. No consume maná: se limita solo con el cooldown.
/// Ejemplo con los valores por defecto: 12 flechas = 4 ráfagas de 3.
/// </summary>
[CreateAssetMenu(fileName = "HunterBarrage", menuName = "RPG/Abilities/Hunter Barrage")]
public class HunterBarrageAbility : SpecialAbility
{
    [Header("Daño")]
    [Tooltip("Multiplica el stat Attack del jugador. Se aplica a CADA flecha.")]
    [SerializeField, Min(0f)] private float damageMultiplier = 1.3f;

    [Header("Ráfagas")]
    [Tooltip("Cantidad total de flechas.")]
    [SerializeField, Min(1)] private int totalArrows = 12;

    [Tooltip("Flechas por ráfaga. Entre ráfagas hay una pausa un poco más larga.")]
    [SerializeField, Min(1)] private int arrowsPerBurst = 3;

    [Tooltip("Segundos entre una flecha y la siguiente dentro de la misma ráfaga.")]
    [SerializeField, Min(0.01f)] private float delayBetweenArrows = 0.06f;

    [Tooltip("Segundos entre el final de una ráfaga y el comienzo de la siguiente.")]
    [SerializeField, Min(0.01f)] private float delayBetweenBursts = 0.3f;

    [Tooltip("Dispersión máxima de cada flecha, en grados (la primera de cada ráfaga sale recta).")]
    [SerializeField, Range(0f, 30f)] private float spreadAngle = 3f;

    public float DamageMultiplier => damageMultiplier;
    public int TotalArrows => totalArrows;
    public int ArrowsPerBurst => arrowsPerBurst;
    public float DelayBetweenArrows => delayBetweenArrows;
    public float DelayBetweenBursts => delayBetweenBursts;
    public float SpreadAngle => spreadAngle;

    /// <summary>Segundos que tarda en salir la última flecha desde la primera.</summary>
    public float TotalDuration
    {
        get
        {
            float time = 0f;

            for (int i = 0; i < totalArrows - 1; i++)
            {
                bool endOfBurst = (i + 1) % arrowsPerBurst == 0;
                time += endOfBurst ? delayBetweenBursts : delayBetweenArrows;
            }

            return time;
        }
    }

    /// <summary>El efecto visual de esta habilidad son las propias flechas.</summary>
    public override void PlayVfx(Player caster, Vector3 position, Vector3 direction) { }
}
