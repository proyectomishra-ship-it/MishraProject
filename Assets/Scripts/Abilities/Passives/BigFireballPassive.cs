using UnityEngine;

/// <summary>
/// Mage: la bola de fuego es más grande y atraviesa a los enemigos, golpeando a varios
/// a la vez. Escala el mismo prefab: no necesita arte nuevo.
/// </summary>
[CreateAssetMenu(fileName = "BigFireball", menuName = "RPG/Passives/Big Fireball (Mage)")]
public class BigFireballPassive : PassiveAbility
{
    [Header("Bola de fuego grande")]
    [Tooltip("Multiplica el tamaño del proyectil (visual y zona de impacto).")]
    [SerializeField, Min(1f)] private float sizeMultiplier = 2.5f;

    [Tooltip("Multiplica el daño de cada impacto (1 = igual que antes).")]
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;

    [Tooltip("Si está activo, la bola atraviesa a los enemigos y golpea a cada uno una vez. " +
             "Si no, se detiene en el primero (solo cambia el tamaño).")]
    [SerializeField] private bool pierceEnemies = true;

    public override string DisplayName => "Gran Bola de Fuego";

    public override void ConfigureProjectile(
        Player owner,
        GameObject instance,
        NetworkProjectile projectile,
        ref float damage)
    {
        // Se escala antes de hacerlo aparecer en la red, así los clientes lo ven del mismo tamaño.
        instance.transform.localScale *= sizeMultiplier;

        damage *= damageMultiplier * PlayerSkillUpgrades.Mult(owner, SkillUpgradeType.MageFireballDamage);
        projectile.SetPierce(pierceEnemies);
    }
}
