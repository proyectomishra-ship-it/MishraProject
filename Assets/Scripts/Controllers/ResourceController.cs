using UnityEngine;
using Unity.Netcode;

public class ResourceController : NetworkBehaviour
{
    private Character character;
    private CharacterStats stats;

    public void Initialize(Character character)
    {
        this.character = character;
        this.stats = character.GetStats();
    }

    // -------------------------
    // HEALTH
    // -------------------------

    public void Heal(float amount)
    {
        if (!IsServer)
            return;

        stats.Heal(amount);
    }

    public void TakeDamage(float amount)
    {
        if (!IsServer)
            return;

        stats.TakeDamage(amount);
    }

    // -------------------------
    // MANA
    // -------------------------

    public bool UseMana(float amount)
    {
        if (!IsServer)
            return false;

        return stats.UseMana(amount);
    }

    public void AddMana(float amount)
    {
        if (!IsServer)
            return;

        stats.AddMana(amount);
    }

    // -------------------------
    // STAMINA
    // -------------------------

    public bool UseStamina(float amount)
    {
        if (!IsServer)
            return false;

        return stats.UseStamina(amount);
    }

    public void RecoverStamina(float amount)
    {
        if (!IsServer)
            return;

        stats.RecoverStamina(amount);
    }

    // -------------------------
    // HELPERS
    // -------------------------

    public float GetCurrentHealth()
    {
        return stats.CurrentHealth;
    }

    public float GetCurrentMana()
    {
        return stats.CurrentMana;
    }

    public float GetCurrentStamina()
    {
        return stats.CurrentStamina;
    }

    public float GetMaxHealth()
    {
        return stats.MaxHealth.Value;
    }

    public float GetMaxMana()
    {
        return stats.MaxMana.Value;
    }

    public float GetMaxStamina()
    {
        return stats.Stamina.Value;
    }
}