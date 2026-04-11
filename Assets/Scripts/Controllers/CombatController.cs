using UnityEngine;
using Unity.Netcode;

public class CombatController : NetworkBehaviour
{
    private Character character;
    private CharacterStats stats;
    private TargetingController targeting;

    [SerializeField] private float heavyAttackMultiplier = 2f;
    [SerializeField] private float holdThreshold = 0.4f;

 
    private float attackHeldTime = 0f;
    private bool isHoldingAttack = false;
    private bool heavyConsumed = false;

    public void Initialize(Character character)
    {
        this.character = character;
        this.stats = character.GetStats();
        targeting = character.GetComponent<TargetingController>();
    }



    public void OnAttackPressed()
    {
        if (!IsServer) return;

        isHoldingAttack = true;
        attackHeldTime = 0f;
        heavyConsumed = false;
    }

    public void OnAttackHeld(float deltaTime)
    {
        if (!IsServer) return;
        if (!isHoldingAttack || heavyConsumed) return;

        attackHeldTime += deltaTime;

        if (attackHeldTime >= holdThreshold)
        {
            Character target = targeting?.CurrentTarget;
            HeavyAttack(target);
            heavyConsumed = true;
            isHoldingAttack = false;
        }
    }

    public void OnAttackReleased()
    {
        if (!IsServer) return;
        if (!isHoldingAttack) return;

        isHoldingAttack = false;

        if (!heavyConsumed)
        {
            Character target = targeting?.CurrentTarget;
            Attack(target);
        }
    }

    public void Attack(Character target)
    {
        if (!IsServer) return;
        if (target == null) return;

        float damage = stats.Attack.Value;
        target.TakeDamage(damage, character);

        Debug.Log($"<color=yellow>ATTACK → {target.name} | Daño: {damage}</color>");
    }

    public void HeavyAttack(Character target)
    {
        if (!IsServer) return;
        if (target == null) return;

        float damage = stats.Attack.Value * heavyAttackMultiplier;
        target.TakeDamage(damage, character);

        Debug.Log($"<color=orange>HEAVY ATTACK → {target.name} | Daño: {damage}</color>");
    }

    public void SpecialAttack(Character target)
    {
        if (!IsServer) return;
        if (target == null) return;

        float damage = stats.Attack.Value * 1.5f;
        target.TakeDamage(damage, character);

        Debug.Log($"<color=cyan>SPECIAL ATTACK → {target.name} | Daño: {damage}</color>");
    }
}