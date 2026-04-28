using System;
using UnityEngine;

public class PlayerStats : CharacterStats
{
    // =========================
    // EVENTS 
    // =========================

    public event Action<int, int> OnExperienceChanged;

    // =========================
    // DATA
    // =========================

    private PlayerClassData classData;

    public int Experience { get; private set; }
    public int ExperienceRequired { get; private set; }

    private float experienceGrowthFactor = 1.5f;
    private int baseExperienceRequired = 100;

    // =========================
    // CONSTRUCTOR
    // =========================

    public PlayerStats(CharacterData data, PlayerClassData classData)
        : base(data)
    {
        this.classData = classData;

        ApplyMultipliers();

        Experience = 0;
        ExperienceRequired = CalculateRequiredXP(Level);

        RaiseXPEvent();
    }

    // =========================
    // EXPERIENCE SYSTEM
    // =========================

    public void AddExperience(int amount)
    {
        if (amount <= 0) return;

        Experience += amount;

       
        RaiseXPEvent();

        ProcessLevelUps();
    }

    private void ProcessLevelUps()
    {
       
        while (Experience >= ExperienceRequired)
        {
            Experience -= ExperienceRequired;

            LevelUpInternal();
        }
    }

    private void LevelUpInternal()
    {
        int newLevel = Level + 1;

        SetLevel(newLevel);

        ApplyLevelScaling();

        ExperienceRequired = CalculateRequiredXP(Level);

        RaiseXPEvent();
    }

    private int CalculateRequiredXP(int level)
    {
        return Mathf.RoundToInt(
            baseExperienceRequired * Mathf.Pow(experienceGrowthFactor, level - 1)
        );
    }

    private void RaiseXPEvent()
    {
        OnExperienceChanged?.Invoke(Experience, ExperienceRequired);
    }

    // =========================
    // CLASS SYSTEM
    // =========================

    private void ApplyLevelScaling()
    {
        foreach (var mod in classData.LevelScaling)
        {
            AddBonus(mod.stat, mod.value);
        }
    }

    private void ApplyMultipliers()
    {
        foreach (var mod in classData.Multipliers)
        {
            AddMultiplier(mod.stat, mod.value);
        }
    }

    // =========================
    //  FORCED SYNC
    // =========================

    public void ForceSync()
    {
  
        RaiseAllEvents();
        RaiseXPEvent();
    }
}