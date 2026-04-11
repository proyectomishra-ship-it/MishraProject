using UnityEngine;
using Unity.Netcode;

public class ResourceController : NetworkBehaviour
{
    private Character character;
    private CharacterStats stats;

 
    private NetworkVariable<float> networkHealth = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> networkMana = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> networkResistance = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );


    public float CurrentHealth => networkHealth.Value;
    public float CurrentMana => networkMana.Value;
    public float CurrentResistance => networkResistance.Value;

    public void Initialize(Character character)
    {
        this.character = character;
        this.stats = character.GetStats();
    }

    public override void OnNetworkSpawn()
    {
      
        if (IsServer)
        {
            networkHealth.Value = stats.MaxHealth.Value;
            networkMana.Value = stats.MaxMana.Value;
            networkResistance.Value = stats.Resistance.Value;
        }

     
        networkHealth.OnValueChanged += OnHealthChanged;
        networkMana.OnValueChanged += OnManaChanged;
        networkResistance.OnValueChanged += OnResistanceChanged;
    }

    public override void OnNetworkDespawn()
    {
        
        networkHealth.OnValueChanged -= OnHealthChanged;
        networkMana.OnValueChanged -= OnManaChanged;
        networkResistance.OnValueChanged -= OnResistanceChanged;
    }

    // -------------------------
    // HEALTH
    // -------------------------

    public void Heal(float amount)
    {
        if (!IsServer) return;
        stats.Heal(amount);
        networkHealth.Value = stats.CurrentHealth;
    }

    public void TakeDamage(float amount)
    {
        if (!IsServer) return;
        stats.TakeDamage(amount);
        networkHealth.Value = stats.CurrentHealth;
    }

    // -------------------------
    // MANA
    // -------------------------

    public bool UseMana(float amount)
    {
        if (!IsServer) return false;
        bool success = stats.UseMana(amount);
        if (success) networkMana.Value = stats.CurrentMana;
        return success;
    }

    public void AddMana(float amount)
    {
        if (!IsServer) return;
        stats.AddMana(amount);
        networkMana.Value = stats.CurrentMana;
    }

    // -------------------------
    // RESISTANCE
    // -------------------------

    public bool UseResistance(float amount)
    {
        if (!IsServer) return false;
        bool success = stats.UseResistance(amount);
        if (success) networkResistance.Value = stats.CurrentResistance;
        return success;
    }

    public void RecoverResistance(float amount)
    {
        if (!IsServer) return;
        stats.RecoverResistance(amount);
        networkResistance.Value = stats.CurrentResistance;
    }

  

    private void OnHealthChanged(float previousValue, float newValue)
    {
        OnHealthUpdated(previousValue, newValue);
    }

    private void OnManaChanged(float previousValue, float newValue)
    {
        OnManaUpdated(previousValue, newValue);
    }

    private void OnResistanceChanged(float previousValue, float newValue)
    {
        OnResistanceUpdated(previousValue, newValue);
    }

  
    protected virtual void OnHealthUpdated(float previousValue, float newValue) { }
    protected virtual void OnManaUpdated(float previousValue, float newValue) { }
    protected virtual void OnResistanceUpdated(float previousValue, float newValue) { }
}