using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class DamageReceiver : NetworkBehaviour
{
    private Character character;

    private Dictionary<Character, float> damageContributors =
        new Dictionary<Character, float>();

    // =========================
    // INITIALIZE
    // =========================

    public void Initialize(Character character)
    {
        this.character = character;
    }

    // =========================
    // GETTERS
    // =========================

    public Dictionary<Character, float> GetDamageContributors()
    {
        return damageContributors;
    }

    // =========================
    // DAMAGE SYSTEM
    // =========================

    public void TakeDamage(AttackData attackData)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "[DamageReceiver] llamado en cliente");

            return;
        }

        if (character == null)
        {
            Debug.LogError(
                "[DamageReceiver] Character null");

            return;
        }

        if (character.GetStats().CurrentHealth <= 0)
            return;

        Character attacker =
            attackData.Attacker;

        Debug.Log(
            $"<color=red>[Damage]</color> " +
            $"{character.name} recibe " +
            $"{attackData.Damage} " +
            $"de {attacker?.name} " +
            $"[{attackData.DamageType}]"
        );

        // =========================
        // DAMAGE CONTRIBUTORS
        // =========================

        if (attacker != null)
        {
            if (!damageContributors.ContainsKey(attacker))
            {
                damageContributors[attacker] = 0f;
            }

            damageContributors[attacker] +=
                attackData.Damage;
        }

        // =========================
        // APPLY DAMAGE
        // =========================

        character
            .GetResourceController()
            .TakeDamage(attackData.Damage);

        character.HandleDamaged(attacker);

        // =========================
        // DEATH
        // =========================

        if (character.GetStats().CurrentHealth <= 0)
        {
            Debug.Log(
                $"<color=magenta>{character.name} MURIÓ</color>");

            character.HandleDeath();
        }
    }

    // =========================
    // LEGACY COMPATIBILITY
    // =========================

    public void TakeDamage(
        float amount,
        Character attacker)
    {
        AttackData data =
            new AttackData
            {
                Attacker = attacker,
                Target = character,
                Damage = amount,
                DamageType = DamageType.Physical,
                IsCritical = false,
                IsHeavy = false,
                HitPoint = character.transform.position
            };

        TakeDamage(data);
    }
}