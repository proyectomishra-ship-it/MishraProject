using UnityEngine;
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

    public NetworkVariable<float> NetResistance = new();
    public NetworkVariable<float> NetMaxResistance = new();

    public NetworkVariable<int> NetLevel = new();

    public NetworkVariable<int> NetXP = new();
    public NetworkVariable<int> NetXPRequired = new();

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

        if (IsServer)
        {
            SubscribeToStats();
            ForceFullSync();
        }
    }

    // =========================
    // SERVER -> STATS
    // =========================

    private void SubscribeToStats()
    {
        stats.OnHealthChanged += OnHealthChanged;
        stats.OnManaChanged += OnManaChanged;
        stats.OnResistanceChanged += OnResistanceChanged;
        stats.OnLevelChanged += OnLevelChanged;

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
        Debug.Log($"[StatsSync] HP Sync {current}/{max}");
    }

    private void OnManaChanged(float current, float max)
    {
        NetMana.Value = current;
        NetMaxMana.Value = max;
    }

    private void OnResistanceChanged(float current, float max)
    {
        NetResistance.Value = current;
        NetMaxResistance.Value = max;
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

        NetResistance.Value = stats.CurrentResistance;
        NetMaxResistance.Value = stats.Resistance.Value;

        NetLevel.Value = stats.Level;

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
        if (!IsServer || stats == null)
            return;

        stats.OnHealthChanged -= OnHealthChanged;
        stats.OnManaChanged -= OnManaChanged;
        stats.OnResistanceChanged -= OnResistanceChanged;
        stats.OnLevelChanged -= OnLevelChanged;

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
        stats.OnResistanceChanged -= OnResistanceChanged;
        stats.OnLevelChanged -= OnLevelChanged;

        if (stats is PlayerStats playerStats)
        {
            playerStats.OnExperienceChanged -= OnXPChanged;
        }
    }
}