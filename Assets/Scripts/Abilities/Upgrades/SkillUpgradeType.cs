/// <summary>
/// Tipos de mejora que se ofrecen al subir de nivel (después de desbloquear las habilidades).
/// IMPORTANTE: no reordenar ni borrar valores: el número se usa como índice en la red y en el asset
/// SkillUpgradeDatabase. Para agregar una mejora nueva, ponerla AL FINAL.
///
/// Cada mejora pertenece a UNA clase y a UNA habilidad, así el texto nunca es ambiguo
/// (ej: "Onda Expansiva: +radio" en vez de "+daño de tu habilidad").
/// </summary>
public enum SkillUpgradeType
{
    // ---- Genéricas del sistema anterior: YA NO SE OFRECEN (se dejan para no romper el orden) ----
    PassivePower = 0,
    SpecialDamage = 1,
    SpecialCooldown = 2,
    SpecialArea = 3,
    SpecialRange = 4,
    BarrageArrows = 5,
    ProjectileSize = 6,
    ProjectileSpeed = 7,
    ProjectileRange = 8,

    // ---- Mage: Onda Expansiva (especial) ----
    MageShockwaveDamage = 9,
    MageShockwaveRadius = 10,
    MageShockwaveCooldown = 11,
    MageShockwaveManaCost = 12,

    // ---- Mage: Gran Bola de Fuego (pasiva) ----
    MageFireballDamage = 13,
    MageFireballSpeed = 14,
    MageFireballRange = 15,
    MageFireballSize = 16,

    // ---- Warrior: Hoja de Energía (especial) ----
    WarriorBladeDamage = 17,
    WarriorBladeLength = 18,
    WarriorBladeArc = 19,
    WarriorBladeCooldown = 20,

    // ---- Warrior: Aura de Guerra (pasiva) ----
    WarriorAuraArmor = 21,
    WarriorAuraDamage = 22,

    // ---- Hunter: Lluvia de Flechas (especial) ----
    HunterBarrageDamage = 23,
    HunterBarrageArrows = 24,
    HunterBarrageCooldown = 25,

    // ---- Hunter: Flecha Explosiva (pasiva) ----
    HunterExplosionDamage = 26,
    HunterExplosionRadius = 27,
    HunterArrowSpeed = 28,
    HunterArrowRange = 29
    // OJO: no hay mejora de tamaño de flecha a propósito: deforma el modelo.
}

/// <summary>Clase a la que pertenece una mejora.</summary>
public enum UpgradeClass
{
    None,
    Mage,
    Warrior,
    Hunter
}

public static class SkillUpgradeTypeInfo
{
    /// <summary>Las 9 genéricas del sistema viejo: nunca se ofrecen ni se consultan.</summary>
    public static bool IsLegacy(this SkillUpgradeType type) => (int)type <= (int)SkillUpgradeType.ProjectileRange;

    /// <summary>
    /// Mejoras que REDUCEN un valor (recarga, costo de maná). Se aplican multiplicando
    /// (1 - x)^n, así nunca llegan a 0. El resto suma: 1 + x * n.
    /// </summary>
    public static bool IsReduction(this SkillUpgradeType type)
    {
        switch (type)
        {
            case SkillUpgradeType.SpecialCooldown:
            case SkillUpgradeType.MageShockwaveCooldown:
            case SkillUpgradeType.MageShockwaveManaCost:
            case SkillUpgradeType.WarriorBladeCooldown:
            case SkillUpgradeType.HunterBarrageCooldown:
                return true;

            default:
                return false;
        }
    }

    public static UpgradeClass ClassOf(this SkillUpgradeType type)
    {
        switch (type)
        {
            case SkillUpgradeType.MageShockwaveDamage:
            case SkillUpgradeType.MageShockwaveRadius:
            case SkillUpgradeType.MageShockwaveCooldown:
            case SkillUpgradeType.MageShockwaveManaCost:
            case SkillUpgradeType.MageFireballDamage:
            case SkillUpgradeType.MageFireballSpeed:
            case SkillUpgradeType.MageFireballRange:
            case SkillUpgradeType.MageFireballSize:
                return UpgradeClass.Mage;

            case SkillUpgradeType.WarriorBladeDamage:
            case SkillUpgradeType.WarriorBladeLength:
            case SkillUpgradeType.WarriorBladeArc:
            case SkillUpgradeType.WarriorBladeCooldown:
            case SkillUpgradeType.WarriorAuraArmor:
            case SkillUpgradeType.WarriorAuraDamage:
                return UpgradeClass.Warrior;

            case SkillUpgradeType.HunterBarrageDamage:
            case SkillUpgradeType.HunterBarrageArrows:
            case SkillUpgradeType.HunterBarrageCooldown:
            case SkillUpgradeType.HunterExplosionDamage:
            case SkillUpgradeType.HunterExplosionRadius:
            case SkillUpgradeType.HunterArrowSpeed:
            case SkillUpgradeType.HunterArrowRange:
                return UpgradeClass.Hunter;

            default:
                return UpgradeClass.None;
        }
    }
}
