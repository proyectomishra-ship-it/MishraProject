using UnityEngine;

public static class ExperienceCalculator
{
    private const float growthFactor = 1.08f;
    private const float minModifier = 0.1f;
    private const float maxModifier = 5f;

    public static int CalculateXP(
        int enemyBaseReward,
        float classMultiplier,
        int enemyLevel,
        int playerLevel)
    {
        int levelDifference = enemyLevel - playerLevel;

        float levelModifier = Mathf.Pow(growthFactor, levelDifference);
        levelModifier = Mathf.Clamp(levelModifier, minModifier, maxModifier);

        float finalXP = enemyBaseReward * classMultiplier * levelModifier;

        return Mathf.RoundToInt(finalXP);
    }
}