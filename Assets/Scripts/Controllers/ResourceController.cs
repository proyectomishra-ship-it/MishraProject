using UnityEngine;

public class ResourceController : MonoBehaviour
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

    public void TakeDamage(float amount)
    {
        float defense = stats.Defense.Value;
        float finalDamage = Mathf.Max(amount - defense, 0);

        stats.CurrentHealth -= finalDamage;
        stats.CurrentHealth = Mathf.Clamp(stats.CurrentHealth, 0, stats.MaxHealth.Value);
    }

    public void Heal(float amount)
    {
        stats.CurrentHealth += amount;
        stats.CurrentHealth = Mathf.Clamp(stats.CurrentHealth, 0, stats.MaxHealth.Value);
    }

    // -------------------------
    // MANA
    // -------------------------

    public bool UseMana(float amount)
    {
        if (stats.CurrentMana < amount)
            return false;

        stats.CurrentMana -= amount;
        return true;
    }

    public void AddMana(float amount)
    {
        stats.CurrentMana += amount;
        stats.CurrentMana = Mathf.Clamp(stats.CurrentMana, 0, stats.MaxMana.Value);
    }

    // -------------------------
    // RESISTANCE (Stamina)
    // -------------------------

    public bool UseResistance(float amount)
    {
        if (stats.CurrentResistance < amount)
            return false;

        stats.CurrentResistance -= amount;
        return true;
    }

    public void RecoverResistance(float amount)
    {
        stats.CurrentResistance += amount;
        stats.CurrentResistance = Mathf.Clamp(stats.CurrentResistance, 0, stats.Resistance.Value);
    }
}