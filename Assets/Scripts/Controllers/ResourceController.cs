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

    private NetworkVariable<float> networkStamina = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );


    public float CurrentHealth => networkHealth.Value;
    public float CurrentMana => networkMana.Value;
    public float CurrentStamina => networkStamina.Value;

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
            networkStamina.Value = stats.Stamina.Value;
        }

     
        networkHealth.OnValueChanged += OnHealthChanged;
        networkMana.OnValueChanged += OnManaChanged;
        networkStamina.OnValueChanged += OnStaminaChanged;
    }

    public override void OnNetworkDespawn()
    {
        
        networkHealth.OnValueChanged -= OnHealthChanged;
        networkMana.OnValueChanged -= OnManaChanged;
        networkStamina.OnValueChanged -= OnStaminaChanged;
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
    // Stamina
    // -------------------------

    public bool UseStamina(float amount)
    {
        if (!IsServer) return false;
        bool success = stats.UseStamina(amount);
        if (success) networkStamina.Value = stats.CurrentStamina;
        return success;
    }

    public void RecoverStamina(float amount)
    {
        if (!IsServer) return;
        stats.RecoverStamina(amount);
        networkStamina.Value = stats.CurrentStamina;
    }

  

    private void OnHealthChanged(float previousValue, float newValue)
    {
        OnHealthUpdated(previousValue, newValue);
    }

    private void OnManaChanged(float previousValue, float newValue)
    {
        OnManaUpdated(previousValue, newValue);
    }

    private void OnStaminaChanged(float previousValue, float newValue)
    {
        OnStaminaUpdated(previousValue, newValue);
    }

  
    protected virtual void OnHealthUpdated(float previousValue, float newValue) { }
    protected virtual void OnManaUpdated(float previousValue, float newValue) { }
    protected virtual void OnStaminaUpdated(float previousValue, float newValue) { }
}