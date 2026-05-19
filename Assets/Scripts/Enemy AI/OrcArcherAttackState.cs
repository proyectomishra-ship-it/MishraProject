using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class OrcArcherAttackState : EnemyStateAttack
{
    private OrcArcherAIController archerAI;

    private float repositionTimer;
    private float repositionInterval = 1.5f;

    public OrcArcherAttackState(Enemy enemy, OrcArcherAIController ai)
        : base(enemy, ai, attackCooldown: 2f)
    {
        archerAI = ai;
    }

    public override void OnEnter()
    {
        base.OnEnter();

        repositionTimer = repositionInterval;

        Debug.Log($"[{enemy.name}][Archer] Buscando cobertura");
    }

    public override void OnExit()
    {
        if (ai.CurrentTarget != null)
            CombatSlotManager.Instance?.RemoveRanged(ai, ai.CurrentTarget.transform);

        Debug.Log($"[{enemy.name}][Archer] Exit Attack");
    }

    public override void OnUpdate()
    {
        if (ai.ShouldFlee)
        {
            ai.StateMachine.ChangeState(ai.FleeState);
            return;
        }

        if (ai.CurrentTarget == null) return;

        repositionTimer += Time.deltaTime;

        if (repositionTimer >= repositionInterval)
        {
            repositionTimer = 0f;
            UpdateSlotPosition();
        }

        base.OnUpdate();
    }

    protected override void PerformAttack()
    {
        if (ai.CurrentTarget == null) return;

        // -------------------------
        // SIN PREFAB -> fallback melee
        // -------------------------
        if (archerAI.ArrowPrefab == null)
        {
            Debug.LogWarning($"[{enemy.name}][Archer] Sin ArrowPrefab -> ataque básico");

            enemy.OnAttackPressed();
            enemy.OnAttackReleased();

            return;
        }

        // -------------------------
        // SOLO SERVER SPAWNEA
        // -------------------------
        if (!enemy.IsServer)
        {
            Debug.LogWarning($"[{enemy.name}][Archer] Intento de spawn en cliente (IGNORADO)");
            return;
        }

        Vector3 spawnPos = enemy.transform.position + Vector3.up * 1.5f;

        Vector3 direction = (
            ai.CurrentTarget.transform.position + Vector3.up - spawnPos
        ).normalized;

        float damage = enemy.GetStats().Attack.Value;

        GameObject instance = Object.Instantiate(
            archerAI.ArrowPrefab,
            spawnPos,
            Quaternion.LookRotation(direction)
        );

        Debug.Log($"[{enemy.name}][Archer] Flecha instanciada");

        // -------------------------
        // NETWORK OBJECT
        // -------------------------
        NetworkObject netObj = instance.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            netObj.Spawn(true);
            Debug.Log($"[{enemy.name}][Archer] NetworkObject Spawn OK");
        }
        else
        {
            Debug.LogError($"[{enemy.name}][Archer] ArrowPrefab SIN NetworkObject");
        }

        // -------------------------
        // PROJECTILE INIT
        // -------------------------
        NetworkProjectile projectile = instance.GetComponent<NetworkProjectile>();

        if (projectile != null)
        {
            projectile.Initialize(enemy, damage, direction);

            Debug.Log($"[{enemy.name}][Archer] Projectile inicializado -> dmg {damage}");
        }
        else
        {
            Debug.LogError($"[{enemy.name}][Archer] Falta NetworkProjectile en prefab");
        }
    }

    // -------------------------
    // POSICIONAMIENTO
    // -------------------------

    private void UpdateSlotPosition()
    {
        if (ai.CurrentTarget == null) return;

        if (CombatSlotManager.Instance != null)
        {
            Vector3 slotPos = CombatSlotManager.Instance.GetRangedSlotPosition(
                ai,
                ai.CurrentTarget.transform,
                archerAI.PreferredCombatDistance
            );

            if (NavMesh.SamplePosition(
                slotPos,
                out NavMeshHit slotHit,
                archerAI.PreferredCombatDistance,
                NavMesh.AllAreas))
            {
                ai.Agent.SetDestination(slotHit.position);
                return;
            }
        }

        TryPositionBehindAlly();
    }

    private void TryPositionBehindAlly()
    {
        if (ai.CurrentTarget == null) return;

        Collider[] nearby = Physics.OverlapSphere(
            enemy.transform.position,
            15f,
            LayerMask.GetMask("Enemy")
        );

        Transform bestAlly = null;
        float bestScore = float.MinValue;

        foreach (var col in nearby)
        {
            Enemy potentialAlly = col.GetComponent<Enemy>();
            if (potentialAlly == null || potentialAlly == enemy) continue;

            Vector3 toPlayer = (
                ai.CurrentTarget.transform.position - enemy.transform.position
            ).normalized;

            Vector3 toAlly = (
                potentialAlly.transform.position - enemy.transform.position
            ).normalized;

            float score = Vector3.Dot(toPlayer, toAlly);

            if (score > bestScore)
            {
                bestScore = score;
                bestAlly = potentialAlly.transform;
            }
        }

        if (bestAlly != null)
        {
            Vector3 dirFromPlayer = (
                bestAlly.position - ai.CurrentTarget.transform.position
            ).normalized;

            Vector3 coverPos = bestAlly.position + dirFromPlayer * 2f;

            if (NavMesh.SamplePosition(coverPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                ai.Agent.SetDestination(hit.position);
        }
        else
        {
            float dist = Vector3.Distance(
                enemy.transform.position,
                ai.CurrentTarget.transform.position
            );

            if (dist < archerAI.PreferredCombatDistance * 0.6f)
            {
                Vector3 away = (
                    enemy.transform.position - ai.CurrentTarget.transform.position
                ).normalized;

                Vector3 retreatPos = enemy.transform.position
                    + away * archerAI.PreferredCombatDistance;

                if (NavMesh.SamplePosition(
                    retreatPos,
                    out NavMeshHit hit,
                    archerAI.PreferredCombatDistance,
                    NavMesh.AllAreas))
                {
                    ai.Agent.SetDestination(hit.position);
                }
            }
        }
    }
}