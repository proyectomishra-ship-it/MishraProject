using UnityEngine;

/// <summary>
/// Calcula la cantidad de oro que genera un enemigo
/// en función de su recompensa base y su nivel.
/// 
/// A diferencia de la experiencia, el nivel del jugador
/// no interviene en el cálculo.
/// </summary>
public static class GoldCalculator
{
    private const float growthFactor = 1.08f;
    private const float minModifier = 0.1f;
    private const float maxModifier = 5f;

    /// <summary>
    /// Calcula el oro total generado por un enemigo.
    /// 
    /// El goldReward representa la recompensa base del enemigo
    /// en nivel 1. El crecimiento depende únicamente del nivel
    /// del enemigo.
    /// </summary>
    public static int CalculateGold(
        int enemyBaseGold,
        int enemyLevel)
    {
        if (enemyBaseGold <= 0)
            return 0;

        int safeEnemyLevel = Mathf.Max(1, enemyLevel);

        float levelModifier =
            Mathf.Pow(
                growthFactor,
                safeEnemyLevel - 1);

        levelModifier =
            Mathf.Clamp(
                levelModifier,
                minModifier,
                maxModifier);

        float finalGold =
            enemyBaseGold * levelModifier;

        return Mathf.RoundToInt(finalGold);
    }
}