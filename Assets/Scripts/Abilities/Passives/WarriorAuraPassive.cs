using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Warrior: aura con efecto visual que mejora al guerrero y a los aliados cercanos.
/// Armadura y daño se pueden activar/desactivar por separado y sus valores se editan acá.
/// </summary>
[CreateAssetMenu(fileName = "WarriorAura", menuName = "RPG/Passives/Warrior Aura (Warrior)")]
public class WarriorAuraPassive : PassiveAbility
{
    private const string VisualName = "VFX_PassiveAura";

    [Header("Aura")]
    [Tooltip("Radio del aura, en metros.")]
    [SerializeField, Min(1f)] private float radius = 6f;

    [Tooltip("Si está activo, los otros jugadores dentro del radio también reciben los bonus.")]
    [SerializeField] private bool affectAllies = true;

    [Header("Armadura")]
    [SerializeField] private bool armorEnabled = true;
    [Tooltip("Defensa extra (valor plano).")]
    [SerializeField] private float armorBonus = 5f;

    [Header("Daño")]
    [SerializeField] private bool damageEnabled = true;
    [Tooltip("Daño extra como porcentaje (0.15 = +15%).")]
    [SerializeField, Min(0f)] private float damageBonusPercent = 0.15f;

    [Header("Visual")]
    [SerializeField] private bool showVisual = true;
    [SerializeField] private Color color = new Color(1f, 0.8f, 0.25f, 1f);

    // =========================================================
    // GAMEPLAY (servidor)
    // =========================================================

    public override string DisplayName => "Aura de Guerra";

    public override PassiveRuntime CreateRuntime(Player owner) => new AuraRuntime(this, owner);

    private sealed class Applied
    {
        public float armor;
        public float damage;
        public float armorPower = 1f;    // mejoras de 'Aura blindada' con las que se calculó armor
        public float damagePower = 1f;   // mejoras de 'Aura furiosa' con las que se calculó damage
    }

    private sealed class AuraRuntime : PassiveRuntime
    {
        private const float ScanInterval = 0.5f;

        private readonly WarriorAuraPassive aura;
        private readonly Player owner;
        private readonly Dictionary<Player, Applied> applied = new Dictionary<Player, Applied>();
        private float nextScan;

        public AuraRuntime(WarriorAuraPassive aura, Player owner)
        {
            this.aura = aura;
            this.owner = owner;
        }

        public override void Tick(bool active)
        {
            if (!active)
            {
                if (applied.Count > 0)
                    RemoveAll();

                return;
            }

            if (Time.time < nextScan)
                return;

            nextScan = Time.time + ScanInterval;

            var desired = new HashSet<Player>();

            if (owner != null && !owner.IsDead())
                desired.Add(owner);

            if (aura.affectAllies && owner != null)
            {
                float sqrRadius = aura.radius * aura.radius;

                foreach (Player p in Object.FindObjectsByType<Player>(FindObjectsSortMode.None))
                {
                    if (p == null || p == owner || p.IsDead())
                        continue;

                    if ((p.transform.position - owner.transform.position).sqrMagnitude <= sqrRadius)
                        desired.Add(p);
                }
            }

            var toRemove = new List<Player>();

            foreach (var pair in applied)
                if (pair.Key == null || !desired.Contains(pair.Key))
                    toRemove.Add(pair.Key);

            foreach (Player p in toRemove)
                Remove(p);

            // Cada mejora escala una parte del aura: 'Aura blindada' la armadura y 'Aura furiosa' el daño.
            float armorPower = PlayerSkillUpgrades.Mult(owner, SkillUpgradeType.WarriorAuraArmor);
            float damagePower = PlayerSkillUpgrades.Mult(owner, SkillUpgradeType.WarriorAuraDamage);

            foreach (Player p in desired)
            {
                if (!applied.TryGetValue(p, out Applied entry))
                {
                    Add(p, armorPower, damagePower);
                }
                else if (!Mathf.Approximately(entry.armorPower, armorPower) ||
                         !Mathf.Approximately(entry.damagePower, damagePower))
                {
                    // El dueño eligió una mejora nueva: se reaplican los bonus con el valor actualizado.
                    Remove(p);
                    Add(p, armorPower, damagePower);
                }
            }
        }

        public override void Dispose() => RemoveAll();

        private void Add(Player p, float armorPower, float damagePower)
        {
            CharacterStats stats = p.GetStats();

            if (stats == null)
                return;

            var entry = new Applied { armorPower = armorPower, damagePower = damagePower };

            if (aura.armorEnabled && aura.armorBonus != 0f)
            {
                entry.armor = aura.armorBonus * armorPower;
                stats.AddBonus(StatType.Defense, entry.armor);
            }

            if (aura.damageEnabled && aura.damageBonusPercent != 0f)
            {
                entry.damage = aura.damageBonusPercent * damagePower;
                stats.AddMultiplier(StatType.Attack, entry.damage);
            }

            applied[p] = entry;
        }

        private void Remove(Player p)
        {
            if (applied.TryGetValue(p, out Applied entry))
            {
                // Si el jugador ya no existe no hay nada que restaurar.
                if (p != null && p.GetStats() != null)
                {
                    if (entry.armor != 0f) p.GetStats().AddBonus(StatType.Defense, -entry.armor);
                    if (entry.damage != 0f) p.GetStats().AddMultiplier(StatType.Attack, -entry.damage);
                }
            }

            applied.Remove(p);
        }

        private void RemoveAll()
        {
            var keys = new List<Player>(applied.Keys);

            foreach (Player p in keys)
                Remove(p);
        }
    }

    // =========================================================
    // VISUAL (todos los clientes)
    // =========================================================

    public override void SetVisualActive(Player owner, bool active)
    {
        if (owner == null)
            return;

        Transform existing = owner.transform.Find(VisualName);

        if (!active || !showVisual)
        {
            if (existing != null)
                Object.Destroy(existing.gameObject);

            return;
        }

        if (existing != null)
            return;

        GameObject root = new GameObject(VisualName);
        root.transform.SetParent(owner.transform, false);
        root.transform.localPosition = Vector3.zero;

        // Anillo de partículas que suben por el borde del aura.
        ParticleSystem ring = SpecialAbilityVfx.CreateSystem(
            root.transform, "Ring", Quaternion.Euler(90f, 0f, 0f), ParticleSystemRenderMode.Billboard);

        ParticleSystem.MainModule rm = ring.main;
        rm.loop = true;
        rm.simulationSpace = ParticleSystemSimulationSpace.Local;
        rm.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
        rm.startSpeed = 0f;
        rm.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        rm.startColor = Color.white;
        rm.maxParticles = 500;

        ParticleSystem.EmissionModule re = ring.emission;
        re.rateOverTime = Mathf.Clamp(radius * 14f, 20f, 160f);

        ParticleSystem.ShapeModule rs = ring.shape;
        rs.enabled = true;
        rs.shapeType = ParticleSystemShapeType.Circle;
        rs.radius = radius;
        rs.radiusThickness = 0.04f;

        ParticleSystem.VelocityOverLifetimeModule rise = ring.velocityOverLifetime;
        rise.enabled = true;
        rise.space = ParticleSystemSimulationSpace.World;
        rise.x = new ParticleSystem.MinMaxCurve(0f);
        rise.y = new ParticleSystem.MinMaxCurve(0.9f);
        rise.z = new ParticleSystem.MinMaxCurve(0f);

        SpecialAbilityVfx.FadeOut(ring, color);
        ring.Play();

        // Disco translúcido en el suelo que pulsa suavemente.
        ParticleSystem disc = SpecialAbilityVfx.CreateSystem(
            root.transform, "Disc", Quaternion.identity, ParticleSystemRenderMode.HorizontalBillboard);

        disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);

        ParticleSystem.MainModule dm = disc.main;
        dm.loop = true;
        dm.simulationSpace = ParticleSystemSimulationSpace.Local;
        dm.startLifetime = 2.5f;
        dm.startSpeed = 0f;
        dm.startSize = radius * 2f;
        dm.startColor = new Color(color.r, color.g, color.b, 0.2f);
        dm.maxParticles = 5;

        ParticleSystem.EmissionModule de = disc.emission;
        de.rateOverTime = 0.8f;

        SpecialAbilityVfx.DisableShape(disc);
        SpecialAbilityVfx.FadeOut(disc, color);
        disc.Play();
    }
}
