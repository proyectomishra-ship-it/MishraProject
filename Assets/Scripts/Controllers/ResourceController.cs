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

    public void Heal(float amount)
    {
        stats.Heal(amount);
    }

    public void TakeDamage(float amount)
    {
        stats.TakeDamage(amount);
    }

    // -------------------------
    // MANA
    // -------------------------

    public bool UseMana(float amount)
    {
        return stats.UseMana(amount);
    }

    public void AddMana(float amount)
    {
        stats.AddMana(amount);
    }

    // -------------------------
    // RESISTANCE
    // -------------------------

    public bool UseResistance(float amount)
    {
        return stats.UseResistance(amount);
    }

    public void RecoverResistance(float amount)
    {
        stats.RecoverResistance(amount);
    }
}