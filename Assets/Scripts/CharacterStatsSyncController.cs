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
    // CLIENT HANDLERS
    // =========================

    private NetworkVariable<float>.OnValueChangedDelegate onHealthChangedHandler;
    private NetworkVariable<float>.OnValueChangedDelegate onManaChangedHandler;
    private NetworkVariable<int>.OnValueChangedDelegate onXPChangedHandler;

    // =========================
    // INIT
    // =========================

    private void Awake()
    {
        character = GetComponent<Character>();

        if (character == null)
            Debug.LogError($"[StatsSync] Falta Character en {gameObject.name}");
    }

    public override void OnNetworkSpawn()
    {
        stats = character.GetStats();

        if (IsServer)
        {
            SubscribeToStats();
            ForceFullSync();
        }

        if (IsClient)
        {
            SubscribeToNetworkVariables();
        }
    }

    // =========================
    // SERVER -> ESCUCHA STATS
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

    private void OnHealthChanged(float current, float max)
    {
        if (NetHealth.Value != current)
            NetHealth.Value = current;

        if (NetMaxHealth.Value != max)
            NetMaxHealth.Value = max;
    }

    private void OnManaChanged(float current, float max)
    {
        if (NetMana.Value != current)
            NetMana.Value = current;

        if (NetMaxMana.Value != max)
            NetMaxMana.Value = max;
    }

    private void OnResistanceChanged(float current, float max)
    {
        if (NetResistance.Value != current)
            NetResistance.Value = current;

        if (NetMaxResistance.Value != max)
            NetMaxResistance.Value = max;
    }

    private void OnLevelChanged(int level)
    {
        if (NetLevel.Value != level)
            NetLevel.Value = level;
    }

    private void OnXPChanged(int current, int required)
    {
        if (NetXP.Value != current)
            NetXP.Value = current;

        if (NetXPRequired.Value != required)
            NetXPRequired.Value = required;
    }

    // =========================
    // CLIENT -> ESCUCHA NETWORK
    // =========================

    private void SubscribeToNetworkVariables()
    {
        onHealthChangedHandler = (oldVal, newVal) =>
        {
            // TODO: UI Health
        };

        onManaChangedHandler = (oldVal, newVal) =>
        {
            // TODO: UI Mana
        };

        onXPChangedHandler = (oldVal, newVal) =>
        {
            // TODO: UI XP
        };

        NetHealth.OnValueChanged += onHealthChangedHandler;
        NetMana.OnValueChanged += onManaChangedHandler;
        NetXP.OnValueChanged += onXPChangedHandler;
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
    }

    // =========================
    // DESPAWN 
    // =========================

    public override void OnNetworkDespawn()
    {
        if (IsServer && stats != null)
        {
            stats.OnHealthChanged -= OnHealthChanged;
            stats.OnManaChanged -= OnManaChanged;
            stats.OnResistanceChanged -= OnResistanceChanged;
            stats.OnLevelChanged -= OnLevelChanged;

            if (stats is PlayerStats playerStats)
            {
                playerStats.OnExperienceChanged -= OnXPChanged;
            }
        }

        if (IsClient)
        {
            if (onHealthChangedHandler != null)
                NetHealth.OnValueChanged -= onHealthChangedHandler;

            if (onManaChangedHandler != null)
                NetMana.OnValueChanged -= onManaChangedHandler;

            if (onXPChangedHandler != null)
                NetXP.OnValueChanged -= onXPChangedHandler;
        }
    }

    // =========================
    // DESTROY 
    // =========================

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (stats == null) return;

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