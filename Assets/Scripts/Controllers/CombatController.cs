using UnityEngine;
using System.Collections;

public class CombatController : MonoBehaviour
{
    private Character character;
    private bool canUseSpecial = true;

    [SerializeField] private float specialCooldown = 5f;

    public void Initialize(Character character)
    {
        this.character = character;
    }

    public void Attack(Character target)
    {
        if (target == null) return;

        float damage = character.GetStats().Attack.Value;
        target.TakeDamage(damage, character);
    }

    public void SpecialAttack(Character target)
    {
        if (!canUseSpecial || target == null) return;

        character.StartCoroutine(SpecialCooldown());

        float damage = character.GetStats().Attack.Value * 2f;
        target.TakeDamage(damage, character);
    }

    private IEnumerator SpecialCooldown()
    {
        canUseSpecial = false;
        yield return new WaitForSeconds(specialCooldown);
        canUseSpecial = true;
    }
}