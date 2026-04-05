using UnityEngine;

public class CombatController : MonoBehaviour
{
    private Character character;
    private CharacterStats stats;

    public void Initialize(Character character)
    {
        this.character = character;
        this.stats = character.GetStats();
    }

    public void Attack(Character target)
    {
        if (target == null) return;

        float damage = stats.Attack.Value;

        target.TakeDamage(damage, character);
    }

    public void SpecialAttack(Character target)
    {
        if (target == null) return;

        float damage = stats.Attack.Value * 1.5f;

        target.TakeDamage(damage, character);
    }
}