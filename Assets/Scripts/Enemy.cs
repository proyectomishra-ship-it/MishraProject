using UnityEngine;

public class Enemy : Character
{
    [SerializeField] private int experienceReward = 50;
    [SerializeField] private float classMultiplier = 1f;

    public int GetExperienceReward(int playerLevel)
    {
        return ExperienceCalculator.CalculateXP(
            experienceReward,
            classMultiplier,
            stats.Level,
            playerLevel
        );
    }
    public override void Move(Vector3 direction)
    {
        
    }

    public override void Attack(Character target)
    {
        base.Attack(target);
    }



    private void DistributeExperience()
    {
        float totalDamage = 0f;

        foreach (var entry in damageContributors)
            totalDamage += entry.Value;

        if (totalDamage <= 0f) return;

        foreach (var entry in damageContributors)
        {
            if (entry.Key is Player player)
            {
                float damageShare = entry.Value / totalDamage;

                int baseXP = GetExperienceReward(player.GetLevel());

                int finalXP = Mathf.RoundToInt(baseXP * damageShare);

                player.AddExp(finalXP);
            }
        }
    }

    protected override void Die()
    {
        DistributeExperience();
        base.Die();
    }
}