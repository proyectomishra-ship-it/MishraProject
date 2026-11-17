using UnityEngine;

/// <summary>
/// Hunter: las flechas comunes (ataque básico y pesado) explotan al impactar y dañan
/// a los enemigos de alrededor. La explosión reutiliza la onda de choque ya existente.
/// </summary>
[CreateAssetMenu(fileName = "ExplosiveArrow", menuName = "RPG/Passives/Explosive Arrow (Hunter)")]
public class ExplosiveArrowPassive : PassiveAbility
{
    [Header("Explosión")]
    [Tooltip("Radio de la explosión, en metros.")]
    [SerializeField, Min(0.5f)] private float explosionRadius = 3.5f;

    [Tooltip("Daño de la explosión como fracción del daño de la flecha (0.6 = 60%). " +
             "El enemigo que recibe la flecha no se daña dos veces.")]
    [SerializeField, Min(0f)] private float explosionDamagePercent = 0.6f;

    [Tooltip("Si está activo, la explosión también daña a otros jugadores.")]
    [SerializeField] private bool damagePlayers = false;

    [SerializeField] private Color explosionColor = new Color(1f, 0.5f, 0.1f, 1f);

    public override string DisplayName => "Flecha Explosiva";

    public override void ConfigureProjectile(
        Player owner,
        GameObject instance,
        NetworkProjectile projectile,
        ref float damage)
    {
        // Mejoras propias de esta pasiva: radio y daño de la explosión.
        float radiusMult = PlayerSkillUpgrades.Mult(owner, SkillUpgradeType.HunterExplosionRadius);
        float damageMult = PlayerSkillUpgrades.Mult(owner, SkillUpgradeType.HunterExplosionDamage);

        projectile.SetExplosion(explosionRadius * radiusMult, explosionDamagePercent * damageMult, explosionColor, damagePlayers);
    }
}
