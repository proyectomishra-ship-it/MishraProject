using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lista de mejoras que el jugador puede elegir al subir de nivel (después de desbloquear sus habilidades)
/// y reglas de cuándo y cómo se ofrecen.
///
/// Para editarla desde el Inspector: menú RPG > Skill Upgrades > Crear / actualizar base de mejoras.
/// Crea Assets/Resources/SkillUpgradeDatabase.asset (el juego lo carga solo). Si no existe el asset,
/// el juego usa los valores por defecto de BuildDefaults() y NO se pueden tocar desde el Inspector.
/// </summary>
[CreateAssetMenu(fileName = "SkillUpgradeDatabase", menuName = "RPG/Skill Upgrade Database")]
public class SkillUpgradeDatabase : ScriptableObject
{
    private const string ResourcePath = "SkillUpgradeDatabase";
    private static SkillUpgradeDatabase current;

    /// <summary>Base activa. Se carga desde Resources; si no existe se crea una con valores por defecto.</summary>
    public static SkillUpgradeDatabase Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<SkillUpgradeDatabase>(ResourcePath);

                if (current == null)
                {
                    current = CreateInstance<SkillUpgradeDatabase>();
                    current.hideFlags = HideFlags.DontSave;
                    current.ResetToDefaults();
                }
                else
                {
                    // Un asset viejo puede no tener las mejoras nuevas: se completan en memoria.
                    current.SyncWithDefaults();
                }
            }

            return current;
        }
    }

    [Serializable]
    public class Entry
    {
        [Tooltip("Nombre de la mejora (lo que ve el jugador).")]
        public string displayName;

        [Tooltip("Tipo de mejora. Define a qué clase y habilidad afecta. No cambiar en entradas existentes.")]
        public SkillUpgradeType type;

        [Tooltip("Nombre de la habilidad a la que pertenece. Se muestra arriba de la tarjeta.")]
        public string skillLabel;

        [Tooltip("Texto que ve el jugador. {0} se reemplaza por el valor de la mejora.")]
        [TextArea(1, 2)] public string description;

        [Tooltip("Cuánto aporta cada vez que se elige. 0.05 = 5%. En 'Más flechas' es la cantidad de flechas.")]
        public float valuePerStack = 0.05f;

        [Tooltip("Si está activo, el valor se muestra como porcentaje (0.05 -> 5). Si no, como número.")]
        public bool isPercent = true;

        [Tooltip("Cuántas veces se puede elegir esta mejora.")]
        [Min(1)] public int maxStacks = 10;

        [Tooltip("Probabilidad relativa de aparecer en la oferta. 2 = el doble de frecuente.")]
        [Min(0f)] public float weight = 1f;

        public bool enabled = true;

        /// <summary>Texto de la mejora con su valor ya reemplazado.</summary>
        public string GetDescription()
        {
            float shown = isPercent ? valuePerStack * 100f : valuePerStack;

            return (description ?? string.Empty).Replace(
                "{0}",
                shown.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    [Header("Cuándo se ofrecen")]
    [Tooltip("Primer nivel que da una mejora. 0 = automático: el nivel en que la clase tiene desbloqueadas " +
             "la pasiva y el especial, más 1 (ej: se desbloquean en el 8 -> la primera mejora llega en el 9).")]
    [SerializeField, Min(0)] private int firstPickLevel = 0;

    [Tooltip("Cada cuántos niveles se da una mejora. 1 = en todos los niveles, 2 = un nivel sí y otro no, 3 = cada tres... " +
             "Se cuenta desde el primer nivel con mejora.")]
    [SerializeField, Min(1)] private int levelsPerUpgrade = 1;

    [Header("Reglas de la oferta")]
    [Tooltip("Si está activo, la mejora que elegiste NO aparece en la oferta siguiente " +
             "(no se puede elegir lo mismo dos veces seguidas).")]
    [SerializeField] private bool blockRepeatOfLastPick = true;

    [Header("Pantalla de elección")]
    [Tooltip("Si está activo, también se puede elegir con las teclas 1, 2 y 3 (además del mouse).")]
    [SerializeField] private bool allowNumberKeys = true;

    [Tooltip("Pausa la partida mientras se elige. Solo pausa si estás jugando solo (host sin otros jugadores): " +
             "en multijugador el resto seguiría jugando, así que ahí solo se bloquea TU personaje y se libera el mouse.")]
    [SerializeField] private bool pauseGameWhenSolo = true;

    [Header("Mejoras")]
    [SerializeField] private List<Entry> entries = new List<Entry>();

    public int FirstPickLevel => firstPickLevel;
    public int LevelsPerUpgrade => Mathf.Max(1, levelsPerUpgrade);
    public bool BlockRepeatOfLastPick => blockRepeatOfLastPick;
    public bool AllowNumberKeys => allowNumberKeys;
    public bool PauseGameWhenSolo => pauseGameWhenSolo;
    public IReadOnlyList<Entry> Entries => entries;

    public bool TryGet(SkillUpgradeType type, out Entry entry)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].type == type)
            {
                entry = entries[i];
                return true;
            }
        }

        entry = null;
        return false;
    }

    // Se llama solo al crear el asset desde el menú del editor.
    private void Reset() => ResetToDefaults();

    private void OnValidate() => SyncWithDefaults();

    [ContextMenu("Restaurar valores por defecto")]
    public void ResetToDefaults()
    {
        entries = BuildDefaults();
    }

    /// <summary>
    /// Agrega las mejoras que falten (por ejemplo, un asset creado con la versión anterior) y quita las genéricas
    /// viejas. No toca los valores de las que ya existen, así tu balance se conserva.
    /// </summary>
    [ContextMenu("Agregar mejoras faltantes")]
    public void SyncWithDefaults()
    {
        if (entries == null)
            entries = new List<Entry>();

        entries.RemoveAll(e => e == null || e.type.IsLegacy());

        foreach (Entry def in BuildDefaults())
        {
            bool exists = false;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].type == def.type)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
                entries.Add(def);
        }
    }

    // =========================================================
    // VALORES POR DEFECTO
    // =========================================================

    private static Entry E(string skill, SkillUpgradeType type, string name, string description,
                           float value, int max, bool percent = true, float weight = 1f)
        => new Entry
        {
            skillLabel = skill,
            type = type,
            displayName = name,
            description = description,
            valuePerStack = value,
            maxStacks = max,
            isPercent = percent,
            weight = weight
        };

    private static List<Entry> BuildDefaults()
    {
        const string shockwave = "Onda Expansiva";
        const string fireball = "Gran Bola de Fuego";
        const string blade = "Hoja de Energía";
        const string aura = "Aura de Guerra";
        const string barrage = "Lluvia de Flechas";
        const string explosive = "Flecha Explosiva";

        return new List<Entry>
        {
            // ---------------- MAGE ----------------
            E(shockwave, SkillUpgradeType.MageShockwaveDamage,   "Onda devastadora", "+{0}% de daño de la Onda Expansiva", 0.10f, 10),
            E(shockwave, SkillUpgradeType.MageShockwaveRadius,   "Onda ampliada",    "+{0}% de radio de la Onda Expansiva", 0.10f, 8),
            E(shockwave, SkillUpgradeType.MageShockwaveCooldown, "Onda frecuente",   "-{0}% de recarga de la Onda Expansiva", 0.05f, 8),
            E(shockwave, SkillUpgradeType.MageShockwaveManaCost, "Onda eficiente",   "-{0}% de maná que cuesta la Onda Expansiva", 0.08f, 6),

            E(fireball,  SkillUpgradeType.MageFireballDamage,    "Fuego intenso",    "+{0}% de daño de la Gran Bola de Fuego", 0.08f, 10),
            E(fireball,  SkillUpgradeType.MageFireballSpeed,     "Fuego veloz",      "+{0}% de velocidad de la bola de fuego", 0.10f, 8),
            E(fireball,  SkillUpgradeType.MageFireballRange,     "Fuego lejano",     "+{0}% de alcance de la bola de fuego", 0.15f, 8),
            E(fireball,  SkillUpgradeType.MageFireballSize,      "Fuego colosal",    "+{0}% de tamaño de la bola de fuego", 0.10f, 4),

            // ---------------- WARRIOR ----------------
            E(blade,     SkillUpgradeType.WarriorBladeDamage,    "Filo letal",       "+{0}% de daño de la Hoja de Energía", 0.10f, 10),
            E(blade,     SkillUpgradeType.WarriorBladeLength,    "Hoja más larga",   "+{0}% de largo de la Hoja de Energía", 0.10f, 8),
            E(blade,     SkillUpgradeType.WarriorBladeArc,       "Barrido amplio",   "+{0}% de apertura del arco de la Hoja de Energía", 0.10f, 6),
            E(blade,     SkillUpgradeType.WarriorBladeCooldown,  "Hoja frecuente",   "-{0}% de recarga de la Hoja de Energía", 0.05f, 8),

            E(aura,      SkillUpgradeType.WarriorAuraArmor,      "Aura blindada",    "+{0}% de armadura que da el Aura de Guerra", 0.10f, 10),
            E(aura,      SkillUpgradeType.WarriorAuraDamage,     "Aura furiosa",     "+{0}% de daño que da el Aura de Guerra", 0.10f, 10),

            // ---------------- HUNTER ----------------
            E(barrage,   SkillUpgradeType.HunterBarrageDamage,   "Lluvia letal",     "+{0}% de daño de cada flecha de la Lluvia de Flechas", 0.10f, 10),
            E(barrage,   SkillUpgradeType.HunterBarrageArrows,   "Más flechas",      "+{0} flechas en la Lluvia de Flechas", 2f, 5, false),
            E(barrage,   SkillUpgradeType.HunterBarrageCooldown, "Lluvia frecuente", "-{0}% de recarga de la Lluvia de Flechas", 0.05f, 8),

            E(explosive, SkillUpgradeType.HunterExplosionDamage, "Pólvora potente",  "+{0}% de daño de la explosión de tus flechas", 0.10f, 10),
            E(explosive, SkillUpgradeType.HunterExplosionRadius, "Explosión amplia", "+{0}% de radio de la explosión de tus flechas", 0.10f, 8),
            E(explosive, SkillUpgradeType.HunterArrowSpeed,      "Flecha veloz",     "+{0}% de velocidad de tus flechas", 0.10f, 8),
            E(explosive, SkillUpgradeType.HunterArrowRange,      "Flecha lejana",    "+{0}% de alcance de tus flechas", 0.15f, 8),
        };
    }
}
