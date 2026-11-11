using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stats de un enemigo. Además de lo de CharacterStats permite cambiar el nivel
/// y reescalar los stats definidos en EnemyData.LevelScaling.
/// El escalado se calcula siempre desde el valor base, así que subir y bajar
/// de nivel repetidas veces no acumula errores.
/// </summary>
public class EnemyStats : CharacterStats
{
    // Un enemigo nunca pierde más de este % de un stat por estar bajo su BaseLevel.
    private const float MaxPenalty = 0.8f;

    private readonly EnemyData data;

    // Bonus total que el escalado de nivel aplica hoy a cada stat.
    private readonly Dictionary<StatType, float> applied = new();

    public EnemyStats(EnemyData data) : base(data)
    {
        this.data = data;
    }

    public void SetEnemyLevel(int newLevel)
    {
        newLevel = Mathf.Max(1, newLevel);

        // Conservar el % de vida actual al cambiar la vida máxima.
        float healthRatio = MaxHealth.Value > 0f
            ? CurrentHealth / MaxHealth.Value
            : 1f;

        int levelsAboveBase = newLevel - data.BaseLevel;

        // Total deseado por stat (por si el asset repite un stat).
        var targets = new Dictionary<StatType, float>();

        foreach (var entry in data.LevelScaling)
        {
            float baseValue = GetBaseValue(entry.stat);
            float bonus = baseValue * entry.percentPerLevel * levelsAboveBase;

            targets.TryGetValue(entry.stat, out float sum);
            targets[entry.stat] = sum + bonus;
        }

        foreach (var kv in targets)
        {
            float baseValue = GetBaseValue(kv.Key);
            float target = Mathf.Max(kv.Value, -baseValue * MaxPenalty);

            applied.TryGetValue(kv.Key, out float current);

            float delta = target - current;

            if (Mathf.Approximately(delta, 0f))
                continue;

            AddBonus(kv.Key, delta);
            applied[kv.Key] = target;
        }

        if (CurrentHealth > 0f)
            SetHealth(MaxHealth.Value * healthRatio);

        // Dispara OnLevelChanged -> CharacterStatsSyncController -> NetLevel.
        SetLevel(newLevel);
    }

    private float GetBaseValue(StatType type)
    {
        switch (type)
        {
            case StatType.Attack:         return data.Attack;
            case StatType.AttackRange:    return data.AttackRange;
            case StatType.Defense:        return data.Defense;
            case StatType.MaxHealth:      return data.MaxHealth;
            case StatType.MaxMana:        return data.MaxMana;
            case StatType.Speed:          return data.Speed;
            case StatType.Agility:        return data.Agility;
            case StatType.CriticalChance: return data.CriticalChance;
            case StatType.Dexterity:      return data.Dexterity;
            case StatType.Intelligence:   return data.Intelligence;
            case StatType.Vitality:       return data.Vitality;
            case StatType.Stamina:        return data.Stamina;
            case StatType.Luck:           return data.Luck;
            default:                      return 0f;
        }
    }
}
