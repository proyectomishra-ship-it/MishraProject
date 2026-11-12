using System;
using UnityEngine;
using Unity.Netcode;

public class Enemy : Character
{
    [Header("Enemy")]
    [SerializeField] private int experienceReward = 50;
    [SerializeField] private int goldReward = 10;
    [SerializeField] private float classMultiplier = 1f;
    public int GoldReward => goldReward;

    [Header("Level Scaling")]
    [Tooltip("Nivel inicial de este enemigo. 0 = usa el BaseLevel del EnemyData.")]
    [Min(0)]
    [SerializeField] private int initialLevel = 0;

    [Tooltip("Activo: el nivel sigue al promedio de los jugadores (+-2). " +
             "Desactivado: el enemigo mantiene siempre su nivel inicial (ej: Acolyte, DemiGod).")]
    [SerializeField] private bool scaleWithPlayers = true;

    [Header("Quest")]
    [Tooltip("Tipo de enemigo utilizado por el sistema de misiones.")]
    [SerializeField] private EnemyTypeData enemyType;

    private EnemyAIController aiController;
    private EnemyGroupMember groupMember;

    protected CombatController combatController;

    private bool deathProcessed;

    // Desfase fijo (-N..+N) respecto al nivel promedio de los jugadores.
    // Se sortea una vez por spawn, asÃ­ un respawn puede salir con otro nivel.
    private int levelOffset;

    public event Action<Enemy> OnEnemyDeath;

    // =========================================================
    // PROPERTIES
    // =========================================================

    public EnemyTypeData EnemyType => enemyType;

    /// <summary>
    /// ID utilizado internamente por el sistema de misiones.
    /// </summary>
    public string EnemyTypeID =>
        enemyType != null
            ? enemyType.EnemyTypeID
            : string.Empty;

    // =========================================================
    // AWAKE
    // =========================================================

    protected override void Awake()
    {
        base.Awake();

        // Nivel inicial configurado en el Inspector (escala los stats desde el EnemyData).
        if (initialLevel > 0 && stats is EnemyStats initialStats)
            initialStats.SetEnemyLevel(initialLevel);

        combatController = GetComponent<CombatController>();

        if (combatController == null)
        {
            Debug.LogError(
                "[Enemy] Falta CombatController",
                this);
        }

        combatController?.Initialize(this);

        aiController = GetComponent<EnemyAIController>();

        if (aiController == null)
        {
            Debug.LogError(
                "[Enemy] Falta EnemyAIController",
                this);
        }

        aiController?.Initialize(this);

        groupMember = GetComponent<EnemyGroupMember>();

        if (groupMember == null)
        {
            Debug.LogError(
                "[Enemy] Falta EnemyGroupMember",
                this);
        }
    }

    // =========================================================
    // STATS / LEVEL SCALING
    // =========================================================

    protected override CharacterStats CreateStats()
    {
        // EnemyStats permite subir el nivel del enemigo y escalar sus stats.
        return characterData is EnemyData enemyData
            ? new EnemyStats(enemyData)
            : base.CreateStats();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer || !scaleWithPlayers)
            return;

        levelOffset = EnemyLevelScaler.RollLevelOffset();
        EnemyLevelScaler.Register(this);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
            EnemyLevelScaler.Unregister(this);

        base.OnNetworkDespawn();
    }

    /// <summary>
    /// Fija el nivel como (promedio de jugadores + desfase propio) y
    /// reescala los stats. Solo en el servidor.
    /// </summary>
    public void ApplyAverageLevel(int averagePlayerLevel)
    {
        if (!IsServer || deathProcessed || !scaleWithPlayers)
            return;

        if (stats is not EnemyStats enemyStats)
            return;

        int target = Mathf.Max(1, averagePlayerLevel + levelOffset);

        if (target == enemyStats.Level)
            return;

        enemyStats.SetEnemyLevel(target);

        Debug.Log(
            $"[EnemyLevel] {name} -> nivel {target} " +
            $"(promedio {averagePlayerLevel}, desfase {levelOffset:+#;-#;0}) | " +
            $"HP:{enemyStats.MaxHealth.Value:F0} " +
            $"ATK:{enemyStats.Attack.Value:F0} " +
            $"DEF:{enemyStats.Defense.Value:F0}");
    }

    // =========================================================
    // REACCIÃ“N AL DAÃ‘O
    // =========================================================

    /// <summary>
    /// Si un jugador golpea a este enemigo (melee o a distancia), todo su grupo
    /// se alerta y va a atacarlo, aunque el golpe haya venido por la espalda
    /// o desde fuera del cono de visiÃ³n.
    /// </summary>
    protected override void OnDamaged(Character attacker)
    {
        base.OnDamaged(attacker);

        if (!IsServer || attacker is not Player)
            return;

        var member = GetComponent<EnemyGroupMember>();
        var group = member != null ? member.CurrentGroup : null;

        if (group != null)
            group.AlertAll(attacker);
        else
            aiController?.ReactToAttack(attacker);
    }

    // =========================================================
    // COMBAT
    // =========================================================

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

    // =========================================================
    // XP
    // =========================================================

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

        var contributors =
            damageReceiver.GetDamageContributors();

        float totalDamage = 0f;

        foreach (var entry in contributors)
        {
            totalDamage += entry.Value;
        }

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
                    Mathf.RoundToInt(
                        baseXP * damageShare);

                player.AddExp(finalXP);

                Debug.Log(
                    $"[XP] {player.name} recibe {finalXP} XP");
            }
        }
    }
    // =========================================================
    // GOLD
    // =========================================================
    private void DistributeGold()
    {
        if (!IsServer)
            return;

        var contributors =
            damageReceiver.GetDamageContributors();

        float totalDamage = 0f;

        foreach (var entry in contributors)
        {
            totalDamage += entry.Value;
        }

        if (totalDamage <= 0f)
            return;

        int totalGold =
            GoldCalculator.CalculateGold(
                goldReward,
                GetLevel());

        if (totalGold <= 0)
            return;

        // Guardamos únicamente los jugadores que participaron.
        var players = new System.Collections.Generic.List<Player>();

        foreach (var entry in contributors)
        {
            if (entry.Key is Player player)
            {
                players.Add(player);
            }
        }

        if (players.Count == 0)
            return;

        int distributedGold = 0;

        for (int i = 0; i < players.Count; i++)
        {
            Player player = players[i];

            float damageShare =
                contributors[player] / totalDamage;

            int playerGold;

            if (i == players.Count - 1)
            {
                // El último jugador recibe el sobrante.
                // Esto garantiza que no se genere ni desaparezca oro.
                playerGold =
                    totalGold - distributedGold;
            }
            else
            {
                playerGold =
                    Mathf.RoundToInt(
                        totalGold * damageShare);

                // Evitamos que el redondeo haga que
                // repartamos más oro del disponible.
                int remainingGold =
                    totalGold - distributedGold;

                playerGold =
                    Mathf.Min(
                        playerGold,
                        remainingGold);
            }

            if (playerGold > 0)
            {
                player.AddGold(playerGold);
                distributedGold += playerGold;

                Debug.Log(
                    $"[GoldReward] {player.name} recibe " +
                    $"{playerGold} oro.");
            }
        }
    }

    // =========================================================
    // QUESTS
    // =========================================================

    private void ReportQuestKill()
    {
        if (!IsServer)
            return;

        // -----------------------------------------------------
        // VALIDACIÓN DEL TIPO DE ENEMIGO
        // -----------------------------------------------------

        if (enemyType == null)
        {
            Debug.LogWarning(
                $"[Enemy] {name} no tiene Enemy Type configurado. " +
                "No se reportará la muerte al sistema de misiones.",
                this);

            return;
        }

        if (string.IsNullOrWhiteSpace(enemyType.EnemyTypeID))
        {
            Debug.LogWarning(
                $"[Enemy] El EnemyTypeData '{enemyType.name}' " +
                $"asignado a {name} no tiene Enemy Type ID configurado. " +
                "No se reportará la muerte al sistema de misiones.",
                this);

            return;
        }

        // -----------------------------------------------------
        // NETWORK QUEST MANAGER
        // -----------------------------------------------------

        if (NetworkQuestManager.Instance == null)
        {
            Debug.LogWarning(
                "[Enemy] NetworkQuestManager no encontrado. " +
                $"No se reportará la muerte de {name}.",
                this);

            return;
        }

        // -----------------------------------------------------
        // REPORTAR KILL
        // -----------------------------------------------------

        NetworkQuestManager.Instance.ReportEnemyKilled(
            enemyType);

        Debug.Log(
            $"[Enemy] Quest kill reportado: " +
            $"{enemyType.DisplayName} " +
            $"(ID: {enemyType.EnemyTypeID})");
    }

    // =========================================================
    // DEATH
    // =========================================================

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

        string enemyTypeName =
            enemyType != null
                ? enemyType.DisplayName
                : "SIN TIPO";

        Debug.Log(
            $"[Enemy] Die -> {name} ({enemyTypeName})");

        // =====================================================
        // GROUP
        // =====================================================

        groupMember?.NotifyDeath();

        // =====================================================
        // XP
        // =====================================================

        DistributeExperience();
        // =====================================================
        // GOLD
        // =====================================================
        DistributeGold();

        // =====================================================
        // DROPS
        // =====================================================

        DropController dropController =
     GetComponent<DropController>();

        if (dropController == null)
        {
            Debug.LogError(
                $"[Enemy] {name} no tiene DropController " +
                "en el mismo GameObject.",
                this);
        }
        else
        {
            Debug.Log(
                $"[Enemy] Solicitando drops para {name}.",
                this);

            dropController.OnEnemyDied();
        }
            // =====================================================
            // QUEST SYSTEM
            // =====================================================

            ReportQuestKill();

        // =====================================================
        // OTHER SYSTEMS
        // =====================================================

        OnEnemyDeath?.Invoke(this);

        // =====================================================
        // BASE DEATH
        // =====================================================

        base.Die();
    }


}
