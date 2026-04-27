using System;
using UnityEngine;

public class CharacterStats
{
    // =========================
    // EVENTS
    // =========================

    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnManaChanged;
    public event Action<float, float> OnResistanceChanged;

    public event Action<int> OnLevelChanged;

    public event Action<StatType, float> OnStatChanged;

    // =========================
    // CORE STATS
    // =========================

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

    // =========================
    // CONSTRUCTOR
    // =========================

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

        RaiseAllEvents();
    }

    // =========================
    // EVENT EMITTERS
    // =========================

    protected void RaiseAllEvents()
    {
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth.Value);
        OnManaChanged?.Invoke(CurrentMana, MaxMana.Value);
        OnResistanceChanged?.Invoke(CurrentResistance, Resistance.Value);
        OnLevelChanged?.Invoke(Level);
    }

    protected void RaiseStatChanged(StatType type, float value)
    {
        OnStatChanged?.Invoke(type, value);
    }

    // =========================
    // STAT ACCESS
    // =========================

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

    // =========================
    // MODIFIERS
    // =========================

    public void AddBonus(StatType type, float value)
    {
        var stat = GetStat(type);
        if (stat == null) return;

        stat.AddBonus(value);
        RaiseStatChanged(type, stat.Value);

        HandleDerivedStats(type);
    }

    public void AddMultiplier(StatType type, float value)
    {
        var stat = GetStat(type);
        if (stat == null) return;

        stat.AddMultiplier(value);
        RaiseStatChanged(type, stat.Value);

        HandleDerivedStats(type);
    }

    // =========================
    // DERIVED STATS 
    // =========================

    private void HandleDerivedStats(StatType type)
    {
        if (type == StatType.MaxHealth)
        {
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0, MaxHealth.Value);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth.Value);
        }

        if (type == StatType.MaxMana)
        {
            CurrentMana = Mathf.Clamp(CurrentMana, 0, MaxMana.Value);
            OnManaChanged?.Invoke(CurrentMana, MaxMana.Value);
        }

        if (type == StatType.Resistance)
        {
            CurrentResistance = Mathf.Clamp(CurrentResistance, 0, Resistance.Value);
            OnResistanceChanged?.Invoke(CurrentResistance, Resistance.Value);
        }
    }

    // =========================
    // HEALTH SYSTEM
    // =========================

    public void TakeDamage(float amount)
    {
        float finalDamage = Mathf.Max(amount - Defense.Value, 0);

        SetHealth(CurrentHealth - finalDamage);
    }

    public void Heal(float amount)
    {
        SetHealth(CurrentHealth + amount);
    }

    protected void SetHealth(float value)
    {
        float old = CurrentHealth;

        CurrentHealth = Mathf.Clamp(value, 0, MaxHealth.Value);

        if (!Mathf.Approximately(old, CurrentHealth))
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth.Value);
    }

    // =========================
    // MANA SYSTEM
    // =========================

    public bool UseMana(float amount)
    {
        if (CurrentMana < amount) return false;

        SetMana(CurrentMana - amount);
        return true;
    }

    public void AddMana(float amount)
    {
        SetMana(CurrentMana + amount);
    }

    protected void SetMana(float value)
    {
        float old = CurrentMana;

        CurrentMana = Mathf.Clamp(value, 0, MaxMana.Value);

        if (!Mathf.Approximately(old, CurrentMana))
            OnManaChanged?.Invoke(CurrentMana, MaxMana.Value);
    }

    // =========================
    // RESISTANCE SYSTEM
    // =========================

    public bool UseResistance(float amount)
    {
        if (CurrentResistance < amount)
            return false;

        SetResistance(CurrentResistance - amount);
        return true;
    }

    public void RecoverResistance(float amount)
    {
        SetResistance(CurrentResistance + amount);
    }

    protected void SetResistance(float value)
    {
        float old = CurrentResistance;

        CurrentResistance = Mathf.Clamp(value, 0, Resistance.Value);

        if (!Mathf.Approximately(old, CurrentResistance))
            OnResistanceChanged?.Invoke(CurrentResistance, Resistance.Value);
    }

    // =========================
    // LEVEL SYSTEM 
    // =========================

    protected void SetLevel(int newLevel)
    {
        if (Level == newLevel) return;

        Level = newLevel;
        OnLevelChanged?.Invoke(Level);
    }
}