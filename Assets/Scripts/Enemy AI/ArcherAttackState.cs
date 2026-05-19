using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class ArcherAttackState : EnemyStateAttack
{
    private readonly float preferredDistance;

    private readonly GameObject projectilePrefab;

    private readonly IEnemyStrategy activeStrategy;

    private readonly float specialAttackCooldown;

    private readonly bool hasSpecial;

    private float specialAttackTimer;

    private CombatController combat;

    public ArcherAttackState(
        Enemy enemy,
        EnemyAIController ai,
        float attackCooldown,
        float preferredDistance,
        GameObject projectilePrefab,
        float specialAttackCooldown,
        bool hasSpecial,
        IEnemyStrategy strategy = null)
        : base(enemy, ai, attackCooldown)
    {
        this.preferredDistance =
            preferredDistance;

        this.projectilePrefab =
            projectilePrefab;

        this.activeStrategy =
            strategy;

        this.specialAttackCooldown =
            specialAttackCooldown;

        this.hasSpecial =
            hasSpecial;
    }

    public override void OnEnter()
    {
        base.OnEnter();

        combat =
            enemy.GetComponent<CombatController>();

        specialAttackTimer =
            specialAttackCooldown;

        activeStrategy?.OnEnter(enemy, ai);

        Debug.Log(
            $"[{enemy.name}] Archer Attack State ENTER");
    }

    public override void OnExit()
    {
        activeStrategy?.OnExit(enemy, ai);

        ai.Agent?.ResetPath();
    }

    public override void OnUpdate()
    {
        if (!enemy.IsServer)
            return;

        if (!IsTargetValid())
        {
            ai.SetTarget(null);

            ai.StateMachine.ChangeState(
                ai.ChaseState);

            return;
        }

        enemy.GetTargetingController()
            ?.ForceTarget(ai.CurrentTarget);

        activeStrategy?.OnUpdate(enemy, ai);

        HandlePositioning();

        base.OnUpdate();

        HandleSpecialAttack();
    }

    private void HandlePositioning()
    {
        if (ai.CurrentTarget == null)
            return;

        float distanceToTarget =
            Vector3.Distance(
                enemy.transform.position,
                ai.CurrentTarget.transform.position);

        if (distanceToTarget <
            preferredDistance * 0.6f)
        {
            Vector3 dirAway =
                (
                    enemy.transform.position -
                    ai.CurrentTarget.transform.position
                ).normalized;

            Vector3 retreatPos =
                enemy.transform.position +
                dirAway * preferredDistance;

            if (NavMesh.SamplePosition(
                retreatPos,
                out NavMeshHit hit,
                preferredDistance,
                NavMesh.AllAreas))
            {
                ai.Agent.SetDestination(hit.position);
            }
        }
        else if (
            distanceToTarget >
            preferredDistance * 1.4f)
        {
            ai.Agent.SetDestination(
                ai.CurrentTarget.transform.position);
        }
        else
        {
            ai.Agent.ResetPath();
        }
    }

    private void HandleSpecialAttack()
    {
        if (!hasSpecial)
            return;

        specialAttackTimer += Time.deltaTime;

        if (specialAttackTimer <
            specialAttackCooldown)
            return;

        specialAttackTimer = 0f;

        Debug.Log(
            $"[{enemy.name}] SPECIAL ATTACK");

        combat?.ExecuteSpecialAttack();
    }

    protected override void PerformAttack()
    {
        if (!enemy.IsServer)
            return;

        if (ai.CurrentTarget == null)
            return;

        enemy.GetTargetingController()
            ?.ForceTarget(ai.CurrentTarget);

        if (projectilePrefab != null)
        {
            Vector3 spawnPos =
                enemy.transform.position +
                Vector3.up * 1.5f;

            Vector3 direction =
                (
                    ai.CurrentTarget.transform.position +
                    Vector3.up -
                    spawnPos
                ).normalized;

            GameObject projGO =
                Object.Instantiate(
                    projectilePrefab,
                    spawnPos,
                    Quaternion.LookRotation(direction));

            NetworkObject netObj =
                projGO.GetComponent<NetworkObject>();

            NetworkProjectile projectile =
                projGO.GetComponent<NetworkProjectile>();

            if (netObj == null || projectile == null)
            {
                Debug.LogError(
                    $"[{enemy.name}] Projectile mal configurado");

                Object.Destroy(projGO);

                return;
            }

            AttackData attackData =
                new AttackData
                {
                    Attacker = enemy,
                    Target = ai.CurrentTarget,
                    Damage = enemy.GetStats().Attack.Value,
                    DamageType = DamageType.Physical,
                    IsCritical = false,
                    IsHeavy = false,
                    HitPoint = spawnPos
                };

            projectile.Initialize(
                attackData,
                direction,
                14f);

            netObj.Spawn();

            Debug.Log(
                $"[{enemy.name}] DISPARO -> {ai.CurrentTarget.name}");
        }
        else
        {
            Debug.Log(
                $"[{enemy.name}] ATAQUE MELEE fallback");

            enemy.OnAttackPressed();
            enemy.OnAttackReleased();
        }
    }
}