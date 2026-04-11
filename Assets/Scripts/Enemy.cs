using UnityEngine;
using Unity.Netcode;

public class Enemy : Character
{
    [SerializeField] private int experienceReward = 50;
    [SerializeField] private float classMultiplier = 1f;

    private EnemyAIController aiController;

    protected override void Awake()
    {
        base.Awake();

        aiController = GetComponent<EnemyAIController>();

      
        if (aiController == null)
            Debug.LogError($"[Enemy] Falta EnemyAIController en el prefab de {gameObject.name}");

        aiController?.Initialize(this);
    }

    public int GetExperienceReward(int playerLevel)
    {
        return ExperienceCalculator.CalculateXP(
            experienceReward,
            classMultiplier,
            GetLevel(),
            playerLevel
        );
    }


    private void DistributeExperience()
    {
        if (!IsServer) return;

        var contributors = damageReceiver.GetDamageContributors();

        float totalDamage = 0f;
        foreach (var entry in contributors)
            totalDamage += entry.Value;

        if (totalDamage <= 0f) return;

        foreach (var entry in contributors)
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