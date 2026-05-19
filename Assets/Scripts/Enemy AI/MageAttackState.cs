using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class MageAttackState : EnemyStateAttack
{
    private float preferredDistance;
    private GameObject spellPrefab;
    private IEnemyStrategy activeStrategy;

    private float specialAttackCooldown;
    private float specialAttackTimer;

    private float heavyAttackCooldown;
    private float heavyAttackTimer;

    private bool hasSpecial;
    private bool hasHeavy;

    public MageAttackState(
        Enemy enemy,
        EnemyAIController ai,
        float attackCooldown,
        float preferredDistance,
        GameObject spellPrefab,
        float heavyAttackCooldown,
        float specialAttackCooldown,
        bool hasHeavy,
        bool hasSpecial,
        IEnemyStrategy strategy = null
    ) : base(enemy, ai, attackCooldown)
    {
        this.preferredDistance = preferredDistance;
        this.spellPrefab = spellPrefab;
        this.activeStrategy = strategy;
        this.heavyAttackCooldown = heavyAttackCooldown;
        this.specialAttackCooldown = specialAttackCooldown;
        this.hasHeavy = hasHeavy;
        this.hasSpecial = hasSpecial;
    }

    public override void OnEnter()
    {
        base.OnEnter();

        heavyAttackTimer = heavyAttackCooldown;
        specialAttackTimer = specialAttackCooldown;

        activeStrategy?.OnEnter(enemy, ai);

        Debug.Log($"[{enemy.name}][Mage] Enter Attack");
    }

    public override void OnUpdate()
    {
        if (ai.CurrentTarget == null) return;

        activeStrategy?.OnUpdate(enemy, ai);

        float distanceToTarget = Vector3.Distance(
            enemy.transform.position,
            ai.CurrentTarget.transform.position);

        
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
        else
        {
            ai.Agent.ResetPath();
        }

       
        base.OnUpdate();

        if (hasHeavy)
        {
            heavyAttackTimer += Time.deltaTime;

            if (heavyAttackTimer >= heavyAttackCooldown)
            {
                heavyAttackTimer = 0f;

                Debug.Log($"[{enemy.name}] HEAVY (input simulado)");

                enemy.OnAttackPressed();
                enemy.OnAttackHeld();
            }
        }

 
        if (hasSpecial)
        {
            specialAttackTimer += Time.deltaTime;

            if (specialAttackTimer >= specialAttackCooldown)
            {
                specialAttackTimer = 0f;

                Debug.Log($"[{enemy.name}] SPECIAL");

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
        if (ai.CurrentTarget == null) return;

        // -------------------------
        // SIN PREFAB -> MELEE VIA INPUT
        // -------------------------
        if (spellPrefab == null)
        {
            Debug.Log($"[{enemy.name}] Ataque melee (input simulado)");

            enemy.OnAttackPressed();
            enemy.OnAttackReleased();

            return;
        }

        // -------------------------
        // SERVER ONLY
        // -------------------------
        if (!enemy.IsServer)
            return;

        Vector3 spawnPos = enemy.transform.position + Vector3.up * 1.5f;

        Vector3 direction = (
            ai.CurrentTarget.transform.position + Vector3.up - spawnPos
        ).normalized;

        float damage = enemy.GetStats().Attack.Value;

        GameObject instance = Object.Instantiate(
            spellPrefab,
            spawnPos,
            Quaternion.LookRotation(direction)
        );

        Debug.Log($"[{enemy.name}] Spell instanciado");

 
        var netObj = instance.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            netObj.Spawn(true);
        }
        else
        {
            Debug.LogError($"[{enemy.name}] Spell sin NetworkObject");
        }

    
        var projectile = instance.GetComponent<NetworkProjectile>();

        if (projectile != null)
        {
            projectile.Initialize(enemy, damage, direction);

            Debug.Log($"[{enemy.name}] Projectile inicializado -> dmg {damage}");
        }
        else
        {
            Debug.LogError($"[{enemy.name}] Falta NetworkProjectile");
        }
    }
}