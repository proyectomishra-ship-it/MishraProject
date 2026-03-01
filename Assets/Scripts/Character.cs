using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Character : MonoBehaviour
{
    [SerializeField] protected CharacterData characterData;

    protected CharacterStats stats;

    // Objetivo actual (para IA / combate)
    protected Character currentTarget;

    // Último atacante (para recompensas, aggro, etc.)
    protected Character lastAttacker;

    [Header("Special Attack")]
    [SerializeField] protected float specialAttackCooldown = 5f;
    protected bool canUseSpecial = true;

    protected Dictionary<Character, float> damageContributors
    = new Dictionary<Character, float>();

    protected virtual void Awake()
    {
        stats = new CharacterStats(characterData);
    }

    #region Combat

    public virtual void Attack(Character target)
    {
        if (target == null) return;

        float damage = stats.Attack.Value;
        target.TakeDamage(damage, this);
    }

    public virtual void SpecialAttack(Character target)
    {
        if (!canUseSpecial || target == null) return;

        StartCoroutine(SpecialCooldown());

        float damage = stats.Attack.Value * 2f;
        target.TakeDamage(damage, this);
    }

    protected virtual IEnumerator SpecialCooldown()
    {
        canUseSpecial = false;
        yield return new WaitForSeconds(specialAttackCooldown);
        canUseSpecial = true;
    }

    public virtual void TakeDamage(float amount, Character attacker)
    {
        if (stats.CurrentHealth <= 0) return;

        currentTarget = attacker;

        if (attacker != null)
        {
            if (!damageContributors.ContainsKey(attacker))
                damageContributors[attacker] = 0f;

            damageContributors[attacker] += amount;
        }

        stats.TakeDamage(amount);

        OnDamaged(attacker);

        if (stats.CurrentHealth <= 0)
            Die();
    }

    protected virtual void OnDamaged(Character attacker)
    {
        
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }

    #endregion

    #region Resources

    public virtual void Heal(float amount)
    {
        stats.Heal(amount);
    }

    public virtual bool UseMana(float amount)
    {
        return stats.UseMana(amount);
    }

    public virtual void AddMana(float amount)
    {
        stats.AddMana(amount);
    }

    #endregion

    #region Getters

    public int GetLevel()
    {
        return stats.Level;
    }

    public Character GetLastAttacker()
    {
        return lastAttacker;
    }

    #endregion

    #region Movement (Agnóstico)

    public virtual void Move(Vector3 direction) { }
    public virtual void Run(Vector3 direction) { }
    public virtual void Jump() { }

    #endregion
}