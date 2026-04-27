using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class ArcherAttackState : EnemyStateAttack
{
    private float preferredDistance;
    private GameObject projectilePrefab;
    private IEnemyStrategy activeStrategy;

    private float specialAttackCooldown;
    private float specialAttackTimer;
    private bool hasSpecial;

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
        this.preferredDistance = preferredDistance;
        this.projectilePrefab = projectilePrefab;
        this.activeStrategy = strategy;
        this.specialAttackCooldown = specialAttackCooldown;
        this.hasSpecial = hasSpecial;
    }

    public override void OnEnter()
    {
        base.OnEnter();

        specialAttackTimer = specialAttackCooldown;

        activeStrategy?.OnEnter(enemy, ai);

        Debug.Log($"[{enemy.name}] Archer Attack State ENTER");
    }

    public override void OnUpdate()
    {
        if (!enemy.IsServer) return;
        if (ai.CurrentTarget == null) return;

      
        enemy.GetComponent<TargetingController>()?.ForceTarget(ai.CurrentTarget);

        activeStrategy?.OnUpdate(enemy, ai);

        float distanceToTarget = Vector3.Distance(
            enemy.transform.position,
            ai.CurrentTarget.transform.position
        );

        // =========================
        // DISTANCIA IDEAL
        // =========================

        if (distanceToTarget < preferredDistance * 0.6f)
        {
            Vector3 dirAway = (enemy.transform.position
                - ai.CurrentTarget.transform.position).normalized;

            Vector3 retreatPos = enemy.transform.position + dirAway * preferredDistance;

            if (NavMesh.SamplePosition(retreatPos, out NavMeshHit hit,
                preferredDistance, NavMesh.AllAreas))
            {
                ai.Agent.SetDestination(hit.position);
            }
        }
        else if (distanceToTarget > preferredDistance * 1.4f)
        {
            ai.Agent.SetDestination(ai.CurrentTarget.transform.position);
        }
        else
        {
            ai.Agent.ResetPath();
        }

        base.OnUpdate();

        // =========================
        // SPECIAL ATTACK
        // =========================

        if (hasSpecial)
        {
            specialAttackTimer += Time.deltaTime;

            if (specialAttackTimer >= specialAttackCooldown)
            {
                specialAttackTimer = 0f;

                Debug.Log($"[{enemy.name}] SPECIAL ATTACK");

                enemy.SpecialAttack();
            }
        }
    }

    public override void OnExit()
    {
        activeStrategy?.OnExit(enemy, ai);
    }

    protected override void PerformAttack()
    {
        if (!enemy.IsServer) return;
        if (ai.CurrentTarget == null) return;

        enemy.GetComponent<TargetingController>()?.ForceTarget(ai.CurrentTarget);

        // =========================
        // RANGED PROJECTILE
        // =========================

        if (projectilePrefab != null)
        {
            Vector3 spawnPos = enemy.transform.position + Vector3.up * 1.5f;

            Vector3 direction = (
                ai.CurrentTarget.transform.position + Vector3.up - spawnPos
            ).normalized;

            GameObject projGO = Object.Instantiate(
                projectilePrefab,
                spawnPos,
                Quaternion.LookRotation(direction)
            );

            var netObj = projGO.GetComponent<NetworkObject>();
            var projectile = projGO.GetComponent<NetworkProjectile>();

            if (netObj == null || projectile == null)
            {
                Debug.LogError($"[{enemy.name}] Projectile mal configurado (falta NetworkObject o NetworkProjectile)");
                return;
            }

            float damage = enemy.GetStats().Attack.Value;

            projectile.Initialize(enemy, damage, direction);

            netObj.Spawn();

            Debug.Log($"[{enemy.name}] DISPARO  {ai.CurrentTarget.name} | Damage: {damage}");
        }
        else
        {
            // =========================
            // FALLBACK MELEE (nuevo sistema)
            // =========================

            Debug.Log($"[{enemy.name}] ATAQUE MELEE fallback");

            enemy.OnAttackPressed();
            enemy.OnAttackReleased();
        }
    }
}