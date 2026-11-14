using UnityEngine;
using System;
using Unity.Netcode;

public class CharacterStatsSyncController : NetworkBehaviour
{
    private Character character;
    private CharacterStats stats;

    // =========================
    // NETWORK VARIABLES
    // =========================

    public NetworkVariable<float> NetHealth = new();
    public NetworkVariable<float> NetMaxHealth = new();

    public NetworkVariable<float> NetMana = new();
    public NetworkVariable<float> NetMaxMana = new();

    public NetworkVariable<float> NetStamina = new();
    public NetworkVariable<float> NetMaxStamina = new();

    public NetworkVariable<int> NetLevel = new();

    public NetworkVariable<int> NetXP = new();
    public NetworkVariable<int> NetXPRequired = new();

    /// <summary>
    /// Valor final de cada StatType (indice = (int)StatType). El equipo solo
    /// se aplica en el servidor, asi que los clientes no pueden calcularlo:
    /// lo leen de aca para mostrarlo (panel de estadisticas).
    /// </summary>
    private readonly NetworkList<float> netStats = new();

    /// <summary>Se dispara en el cliente/host cuando cambia algun stat sincronizado.</summary>
    public event Action OnStatsSynced;

    /// <summary>Valor final del stat (base + equipo, con multiplicadores).</summary>
    public float GetStatValue(StatType type)
    {
        if (IsServer && stats != null)
            return stats.GetStat(type)?.Value ?? 0f;

        int i = (int)type;
        return i >= 0 && i < netStats.Count ? netStats[i] : 0f;
    }

    // =========================
    // INIT
    // =========================

    private void Awake()
    {
        character = GetComponent<Character>();

        if (character == null)
        {
            Debug.LogError(
                $"[StatsSync] Falta Character en {gameObject.name}"
            );
        }
    }

    public override void OnNetworkSpawn()
    {
        stats = character.GetStats();

        if (stats == null)
        {
            Debug.LogError(
                $"[StatsSync] CharacterStats es NULL en {gameObject.name}"
            );
            return;
        }

        netStats.OnListChanged += HandleNetStatsChanged;

        if (IsServer)
        {
            SubscribeToStats();
            ForceFullSync();
        }
    }

    private void HandleNetStatsChanged(NetworkListEvent<float> _) => OnStatsSynced?.Invoke();

    private void OnStatChangedServer(StatType type, float value)
    {
        int i = (int)type;
        if (i >= 0 && i < netStats.Count) netStats[i] = value;
    }

    private void SyncAllStats()
    {
        int count = Enum.GetValues(typeof(StatType)).Length;
        while (netStats.Count < count) netStats.Add(0f);

        foreach (StatType t in Enum.GetValues(typeof(StatType)))
            netStats[(int)t] = stats.GetStat(t)?.Value ?? 0f;
    }

    // =========================
    // SERVER -> STATS
    // =========================

    private void SubscribeToStats()
    {
        stats.OnHealthChanged += OnHealthChanged;
        stats.OnManaChanged += OnManaChanged;
        stats.OnStaminaChanged += OnStaminaChanged;
        stats.OnLevelChanged += OnLevelChanged;
        stats.OnStatChanged += OnStatChangedServer;

        if (stats is PlayerStats playerStats)
        {
            playerStats.OnExperienceChanged += OnXPChanged;
        }
    }

    // =========================
    // EVENTS
    // =========================

    private void OnHealthChanged(float current, float max)
    {
        NetHealth.Value = current;
        NetMaxHealth.Value = max;

    }

    private void OnManaChanged(float current, float max)
    {
        NetMana.Value = current;
        NetMaxMana.Value = max;
    }

    private void OnStaminaChanged(float current, float max)
    {
        NetStamina.Value = current;
        NetMaxStamina.Value = max;
    }

    private void OnLevelChanged(int level)
    {
        NetLevel.Value = level;
    }

    private void OnXPChanged(int current, int required)
    {
        NetXP.Value = current;
        NetXPRequired.Value = required;
    }

    // =========================
    // FULL SYNC
    // =========================

    private void ForceFullSync()
    {
        NetHealth.Value = stats.CurrentHealth;
        NetMaxHealth.Value = stats.MaxHealth.Value;

        NetMana.Value = stats.CurrentMana;
        NetMaxMana.Value = stats.MaxMana.Value;

        NetStamina.Value = stats.CurrentStamina;
        NetMaxStamina.Value = stats.Stamina.Value;

        NetLevel.Value = stats.Level;

        SyncAllStats();

        if (stats is PlayerStats playerStats)
        {
            NetXP.Value = playerStats.Experience;
            NetXPRequired.Value = playerStats.ExperienceRequired;
        }

        Debug.Log("[StatsSync] ForceFullSync");

    }

    // =========================
    // DESPAWN
    // =========================

    public override void OnNetworkDespawn()
    {
        netStats.OnListChanged -= HandleNetStatsChanged;

        if (!IsServer || stats == null)
            return;

        stats.OnHealthChanged -= OnHealthChanged;
        stats.OnManaChanged -= OnManaChanged;
        stats.OnStaminaChanged -= OnStaminaChanged;
        stats.OnLevelChanged -= OnLevelChanged;
        stats.OnStatChanged -= OnStatChangedServer;

        if (stats is PlayerStats playerStats)
        {
            playerStats.OnExperienceChanged -= OnXPChanged;
        }
    }

    // =========================
    // DESTROY
    // =========================

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (stats == null)
            return;

        stats.OnHealthChanged -= OnHealthChanged;
        stats.OnManaChanged -= OnManaChanged;
        stats.OnStaminaChanged -= OnStaminaChanged;
        stats.OnLevelChanged -= OnLevelChanged;
        stats.OnStatChanged -= OnStatChangedServer;

        if (stats is PlayerStats playerStats)
        {
            playerStats.OnExperienceChanged -= OnXPChanged;
        }
    }
}