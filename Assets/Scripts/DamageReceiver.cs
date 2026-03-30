using System.Collections.Generic;
using UnityEngine;

public class DamageReceiver : MonoBehaviour
{
    private Character character;

    private Dictionary<Character, float> damageContributors
        = new Dictionary<Character, float>();
    public Dictionary<Character, float> GetDamageContributors()
    {
        return damageContributors;
    }
    public void Initialize(Character character)
    {
        this.character = character;
    }

    public void TakeDamage(float amount, Character attacker)
    {
        if (character.GetStats().CurrentHealth <= 0) return;

        if (attacker != null)
        {
            if (!damageContributors.ContainsKey(attacker))
                damageContributors[attacker] = 0f;

            damageContributors[attacker] += amount;
        }

        character.GetResourceController().TakeDamage(amount);

        character.HandleDamaged(attacker);

        if (character.GetStats().CurrentHealth <= 0)
            character.HandleDeath();
    }
}