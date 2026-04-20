using UnityEngine;

public class DemiGodAIController : EnemyAIController
{
    private DemigodData demigodData;
    private Enemy demiGodEnemy;

    // Fases según % de vida restante.
    private const float Phase2Threshold = 0.6f; // < 60% → fase 2
    private const float Phase3Threshold = 0.3f; // < 30% → fase 3
    private int currentPhase = 1;

    public void InitializeWithData(Enemy enemy, DemigodData data)
    {
        this.demigodData = data;
        this.demiGodEnemy = enemy;
        Initialize(enemy);
    }

    protected override EnemyStateAttack CreateAttackState()
    {
        if (demigodData == null)
        {
            Debug.LogError("[demigodAIController] demigodData no asignado.");
            return null;
        }

        // Fase 1: melee con flanqueo.
        return BuildPhase1Attack();
    }

    private void Update()
    {
        if (!IsServer) return;

        // Chequear cambio de fase cada frame.
        CheckPhaseTransition();
        StateMachine?.Update();
    }

    private void CheckPhaseTransition()
    {
        var rc = demiGodEnemy.GetResourceController();
        if (rc == null) return;

        float healthPercent = rc.CurrentHealth / demiGodEnemy.GetStats().MaxHealth.Value;

        if (currentPhase == 1 && healthPercent <= Phase2Threshold)
        {
            currentPhase = 2;
            TransitionToPhase2();
        }
        else if (currentPhase == 2 && healthPercent <= Phase3Threshold)
        {
            currentPhase = 3;
            TransitionToPhase3();
        }
    }

    private void TransitionToPhase2()
    {
        Debug.Log($"[demigod] ¡Fase 2! Cambia a ataque mágico.");

        // Fase 2: ataque mágico con flanqueo.
        AttackState = BuildPhase2Attack();

        // Si estaba atacando, transicionar al nuevo estado de ataque.
        if (StateMachine.CurrentState is EnemyStateAttack)
            StateMachine.ChangeState(AttackState);
    }

    private void TransitionToPhase3()
    {
        Debug.Log($"[demigod] ¡Fase 3! Modo berserker.");

        // Fase 3: melee agresivo sin estrategia, máximo daño.
        AttackState = BuildPhase3Attack();

        if (StateMachine.CurrentState is EnemyStateAttack)
            StateMachine.ChangeState(AttackState);
    }

    private EnemyStateAttack BuildPhase1Attack()
    {
        IEnemyStrategy strategy = null;
        if (demigodData.AvailableStrategies.Contains(EnemyStrategyType.Flank))
            strategy = new FlankStrategy();

        return new MeleeAttackState(
            enemy: demiGodEnemy,
            ai: this,
            attackCooldown: demigodData.AttackCooldown,
            heavyAttackCooldown: demigodData.HeavyAttackCooldown,
            specialAttackCooldown: demigodData.SpecialAttackCooldown,
            hasHeavy: demigodData.HasHeavyAttack,
            hasSpecial: demigodData.HasSpecialAttack,
            strategy: strategy
        );
    }

    private EnemyStateAttack BuildPhase2Attack()
    {
        // Fase 2: Mago con cooldowns reducidos.
        return new MageAttackState(
            enemy: demiGodEnemy,
            ai: this,
            attackCooldown: demigodData.AttackCooldown * 0.8f,
            preferredDistance: demigodData.PreferredCombatDistance,
            spellPrefab: demigodData.ProjectilePrefab,
            heavyAttackCooldown: demigodData.HeavyAttackCooldown * 0.8f,
            specialAttackCooldown: demigodData.SpecialAttackCooldown * 0.8f,
            hasHeavy: demigodData.HasHeavyAttack,
            hasSpecial: demigodData.HasSpecialAttack,
            strategy: new FlankStrategy()
        );
    }

    private EnemyStateAttack BuildPhase3Attack()
    {
        // Fase 3: Melee puro sin estrategia, cooldowns mínimos.
        return new MeleeAttackState(
            enemy: demiGodEnemy,
            ai: this,
            attackCooldown: demigodData.AttackCooldown * 0.5f,
            heavyAttackCooldown: demigodData.HeavyAttackCooldown * 0.5f,
            specialAttackCooldown: demigodData.SpecialAttackCooldown * 0.5f,
            hasHeavy: true,
            hasSpecial: true,
            strategy: null
        );
    }
}