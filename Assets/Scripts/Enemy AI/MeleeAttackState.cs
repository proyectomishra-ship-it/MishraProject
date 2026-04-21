using UnityEngine;

public class MeleeAttackState : EnemyStateAttack
{
    private IEnemyStrategy activeStrategy;
    private float heavyAttackCooldown;
    private float heavyAttackTimer;
    private float specialAttackCooldown;
    private float specialAttackTimer;
    private bool hasHeavy;
    private bool hasSpecial;

    public MeleeAttackState(Enemy enemy, EnemyAIController ai,
        float attackCooldown, float heavyAttackCooldown,
        float specialAttackCooldown, bool hasHeavy, bool hasSpecial,
        IEnemyStrategy strategy = null)
        : base(enemy, ai, attackCooldown)
    {
        this.heavyAttackCooldown = heavyAttackCooldown;
        this.specialAttackCooldown = specialAttackCooldown;
        this.hasHeavy = hasHeavy;
        this.hasSpecial = hasSpecial;
        this.activeStrategy = strategy;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        heavyAttackTimer = heavyAttackCooldown;
        specialAttackTimer = specialAttackCooldown;
        activeStrategy?.OnEnter(enemy, ai);
    }

    public override void OnUpdate()
    {
     
        activeStrategy?.OnUpdate(enemy, ai);
        base.OnUpdate();

        if (ai.CurrentTarget == null) return;

        if (hasHeavy)
        {
            heavyAttackTimer += Time.deltaTime;
            if (heavyAttackTimer >= heavyAttackCooldown)
            {
                heavyAttackTimer = 0f;
                enemy.Attack(ai.CurrentTarget); 
                return;
            }
        }

      
        if (hasSpecial)
        {
            specialAttackTimer += Time.deltaTime;
            if (specialAttackTimer >= specialAttackCooldown)
            {
                specialAttackTimer = 0f;
                enemy.SpecialAttack();
                return;
            }
        }
    }

    public override void OnExit()
    {
        activeStrategy?.OnExit(enemy, ai);
    }

    protected override void PerformAttack()
    {
        enemy.Attack(ai.CurrentTarget);
    }
}