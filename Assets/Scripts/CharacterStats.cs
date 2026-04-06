using UnityEngine;

public class CharacterStats
{
    public int Level { get; protected set; }

    public Stat Attack { get; protected set; }
    public Stat AttackRange { get; protected set; }

    public Stat Defense { get; protected set; }
    public Stat Speed { get; protected set; }
    public Stat Agility { get; protected set; }
    public Stat CriticalChance { get; protected set; }
    public Stat Dexterity { get; protected set; }
    public Stat Intelligence { get; protected set; }
    public Stat Vitality { get; protected set; }
    public Stat Resistance { get; protected set; }
    public Stat Luck { get; protected set; }

    public Stat MaxHealth { get; protected set; }
    public float CurrentHealth { get; protected set; }

    public Stat MaxMana { get; protected set; }
    public float CurrentMana { get; protected set; }

    
    public float CurrentResistance { get; protected set; }

    public CharacterStats(CharacterData data)
    {
        Level = data.BaseLevel;

        Attack = new Stat(data.Attack);
        AttackRange = new Stat(data.AttackRange);
        Defense = new Stat(data.Defense);
        Speed = new Stat(data.Speed);
        Agility = new Stat(data.Agility);
        CriticalChance = new Stat(data.CriticalChance);
        Dexterity = new Stat(data.Dexterity);
        Intelligence = new Stat(data.Intelligence);
        Vitality = new Stat(data.Vitality);
        Resistance = new Stat(data.Resistance);
        Luck = new Stat(data.Luck);

        MaxHealth = new Stat(data.MaxHealth);
        MaxMana = new Stat(data.MaxMana);

        CurrentHealth = MaxHealth.Value;
        CurrentMana = MaxMana.Value;

        
        CurrentResistance = Resistance.Value;
    }

    public Stat GetStat(StatType type)
    {
        switch (type)
        {
            case StatType.Attack: return Attack;
            case StatType.AttackRange: return AttackRange;
            case StatType.Defense: return Defense;
            case StatType.MaxHealth: return MaxHealth;
            case StatType.MaxMana: return MaxMana;
            case StatType.Speed: return Speed;
            case StatType.Agility: return Agility;
            case StatType.CriticalChance: return CriticalChance;
            case StatType.Dexterity: return Dexterity;
            case StatType.Intelligence: return Intelligence;
            case StatType.Vitality: return Vitality;
            case StatType.Resistance: return Resistance;
            case StatType.Luck: return Luck;
            default: return null;
        }
    }

    public void AddBonus(StatType type, float value)
    {
        GetStat(type)?.AddBonus(value);
    }

    public void AddMultiplier(StatType type, float value)
    {
        GetStat(type)?.AddMultiplier(value);
    }


    public void TakeDamage(float amount)
    {
        float finalDamage = Mathf.Max(amount - Defense.Value, 0);
        CurrentHealth -= finalDamage;
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0, MaxHealth.Value);
    }

    public void Heal(float amount)
    {
        CurrentHealth += amount;
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0, MaxHealth.Value);
    }


    public bool UseMana(float amount)
    {
        if (CurrentMana < amount) return false;

        CurrentMana -= amount;
        return true;
    }

    public void AddMana(float amount)
    {
        CurrentMana += amount;
        CurrentMana = Mathf.Clamp(CurrentMana, 0, MaxMana.Value);
    }


    public bool UseResistance(float amount)
    {
        if (CurrentResistance < amount)
            return false;

        CurrentResistance -= amount;
        return true;
    }

    public void RecoverResistance(float amount)
    {
        CurrentResistance += amount;
        CurrentResistance = Mathf.Clamp(CurrentResistance, 0, Resistance.Value);
    }
}