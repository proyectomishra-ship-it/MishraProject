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

    public MeleeAttackState(
        Enemy enemy,
        EnemyAIController ai,
        float attackCooldown,
        float heavyAttackCooldown,
        float specialAttackCooldown,
        bool hasHeavy,
        bool hasSpecial,
        IEnemyStrategy strategy = null
    ) : base(enemy, ai, attackCooldown)
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

        Debug.Log($"[{enemy.name}][Melee] Enter Attack");
    }

    public override void OnUpdate()
    {
        if (ai.CurrentTarget == null) return;

        activeStrategy?.OnUpdate(enemy, ai);

        base.OnUpdate();

        // -------------------------
        // HEAVY ATTACK
        // -------------------------
        if (hasHeavy)
        {
            heavyAttackTimer += Time.deltaTime;

            if (heavyAttackTimer >= heavyAttackCooldown)
            {
                heavyAttackTimer = 0f;

                Debug.Log($"[{enemy.name}][Melee] HEAVY Attack (input simulado)");

                // Simula mantener ataque
                enemy.OnAttackPressed();
                enemy.OnAttackHeld();

                return;
            }
        }

        // -------------------------
        // SPECIAL ATTACK
        // -------------------------
        if (hasSpecial)
        {
            specialAttackTimer += Time.deltaTime;

            if (specialAttackTimer >= specialAttackCooldown)
            {
                specialAttackTimer = 0f;

                Debug.Log($"[{enemy.name}][Melee] SPECIAL Attack");

                enemy.SpecialAttack();

                return;
            }
        }
    }

    public override void OnExit()
    {
        activeStrategy?.OnExit(enemy, ai);

        Debug.Log($"[{enemy.name}][Melee] Exit Attack");
    }

    protected override void PerformAttack()
    {
        if (ai.CurrentTarget == null) return;

        Debug.Log($"[{enemy.name}][Melee] LIGHT Attack (input simulado)");

        // Ataque básico = press + release
        enemy.OnAttackPressed();
        enemy.OnAttackReleased();
    }
}