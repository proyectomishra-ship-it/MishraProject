using UnityEngine;

public class PlayerStats : CharacterStats
{
    private PlayerClassData classData;

    public int Experience { get; private set; }
    public int ExperienceRequired { get; private set; }

    private float experienceGrowthFactor = 1.5f;
    private int baseExperienceRequired = 100;

    public PlayerStats(CharacterData data, PlayerClassData classData)
        : base(data)
    {
        this.classData = classData;

        ApplyMultipliers();

        Experience = 0;
        ExperienceRequired = baseExperienceRequired;
    }

    public void AddExperience(int amount)
    {
        Experience += amount;

        while (Experience >= ExperienceRequired)
        {
            Experience -= ExperienceRequired;
            LevelUp();
        }
    }

    private void LevelUp()
    {
        Level++;

        ApplyLevelScaling();

        ExperienceRequired = Mathf.RoundToInt(
            baseExperienceRequired * Mathf.Pow(experienceGrowthFactor, Level - 1)
        );
    }

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
}