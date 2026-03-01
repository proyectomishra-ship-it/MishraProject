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

        ApplyClassScaling();

        ExperienceRequired = Mathf.RoundToInt(
            baseExperienceRequired * Mathf.Pow(experienceGrowthFactor, Level - 1)
        );
    }

    private void ApplyClassScaling()
    {
        Attack.AddBonus(classData.AttackPerLevel);
        Defense.AddBonus(classData.DefensePerLevel);
        MaxHealth.AddBonus(classData.MaxHealthPerLevel);
        MaxMana.AddBonus(classData.MaxManaPerLevel);
        Dexterity.AddBonus(classData.DexterityPerLevel);
        Intelligence.AddBonus(classData.IntelligencePerLevel);
        Vitality.AddBonus(classData.VitalityPerLevel);
        Luck.AddBonus(classData.LuckPerLevel);
    }
}