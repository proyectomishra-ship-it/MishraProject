using System;
using UnityEngine;
using Unity.Netcode;

public class Enemy : Character
{
    [Header("Enemy")]
    [SerializeField] private int experienceReward = 50;
    [SerializeField] private float classMultiplier = 1f;

    [Header("Quest")]
    [Tooltip("Unique ID used by the quest system to identify this enemy type.")]
    [SerializeField] private string enemyTypeID;

    private EnemyAIController aiController;
    private EnemyGroupMember groupMember;

    protected CombatController combatController;

    private bool deathProcessed;

    public event Action<Enemy> OnEnemyDeath;

    protected override void Awake()
    {
        base.Awake();

        combatController = GetComponent<CombatController>();

        if (combatController == null)
            Debug.LogError(
                "[Enemy] Falta CombatController",
                this);

        combatController?.Initialize(this);

        aiController = GetComponent<EnemyAIController>();

        if (aiController == null)
            Debug.LogError(
                "[Enemy] Falta EnemyAIController",
                this);

        aiController?.Initialize(this);

        groupMember = GetComponent<EnemyGroupMember>();

        if (groupMember == null)
            Debug.LogError(
                "[Enemy] Falta EnemyGroupMember",
                this);
    }

    // =========================
    // COMBAT
    // =========================

    public override void OnAttackPressed()
    {
        combatController?.OnAttackPressed();
    }

    public override void OnAttackHeld()
    {
        combatController?.OnAttackHeld(Time.deltaTime);
    }

    public override void OnAttackReleased()
    {
        combatController?.OnAttackReleased();
    }

    public override void SpecialAttack()
    {
        combatController?.SpecialAttack();
    }

    // =========================
    // XP
    // =========================

    public int GetExperienceReward(int playerLevel)
    {
        return ExperienceCalculator.CalculateXP(
            experienceReward,
            classMultiplier,
            GetLevel(),
            playerLevel);
    }

    private void DistributeExperience()
    {
        if (!IsServer)
            return;

        var contributors = damageReceiver.GetDamageContributors();

        float totalDamage = 0f;

        foreach (var entry in contributors)
            totalDamage += entry.Value;

        if (totalDamage <= 0f)
            return;

        foreach (var entry in contributors)
        {
            if (entry.Key is Player player)
            {
                float damageShare =
                    entry.Value / totalDamage;

                int baseXP =
                    GetExperienceReward(player.GetLevel());

                int finalXP =
                    Mathf.RoundToInt(baseXP * damageShare);

                player.AddExp(finalXP);

                Debug.Log(
                    $"[XP] {player.name} recibe {finalXP} XP");
            }
        }
    }

    // =========================
    // QUESTS
    // =========================

    private void ReportQuestKill()
    {
        if (!IsServer)
            return;

        if (string.IsNullOrWhiteSpace(enemyTypeID))
        {
            Debug.LogWarning(
                $"[Enemy] {name} no tiene Enemy Type ID configurado. " +
                "No se reportará la muerte al sistema de misiones.",
                this);

            return;
        }

        if (NetworkQuestManager.Instance == null)
        {
            Debug.LogWarning(
                $"[Enemy] NetworkQuestManager no encontrado. " +
                $"No se reportará la muerte de {name}.",
                this);

            return;
        }

        NetworkQuestManager.Instance.ReportEnemyKilled(
            enemyTypeID);

        Debug.Log(
            $"[Enemy] Quest kill reportado: {enemyTypeID}");
    }

    // =========================
    // DEATH
    // =========================

    protected override void Die()
    {
        if (!IsServer)
        {
            base.Die();
            return;
        }

        if (deathProcessed)
        {
            Debug.LogWarning(
                $"[Enemy] Die() llamado nuevamente para {name}. " +
                "La muerte ya fue procesada.",
                this);

            return;
        }

        deathProcessed = true;

        Debug.Log($"[Enemy] Die -> {name}");

        // =========================
        // GROUP
        // =========================

        groupMember?.NotifyDeath();

        // =========================
        // XP
        // =========================

        DistributeExperience();

        // =========================
        // DROPS
        // =========================

        GetComponent<DropController>()
            ?.OnEnemyDied();

        // =========================
        // QUEST SYSTEM
        // =========================

        ReportQuestKill();

        // =========================
        // OTHER SYSTEMS
        // =========================

        OnEnemyDeath?.Invoke(this);

        // =========================
        // BASE DEATH
        // =========================

        base.Die();
    }
}