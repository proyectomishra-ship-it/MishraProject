using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class GoblinAttackState : EnemyStateAttack
{
    private GoblinAIController controller;
    private NavMeshAgent agent;

    private float repositionInterval = 0.3f;
    private float repositionTimer;

    private float separationRadius = 1.2f;
    private float separationStrength = 1.5f;

    private float feintChance = 0.25f;
    private bool isFeinting;
    private float feintTimer;
    private float feintDuration = 0.5f;

    private float originalSpeed;

    public GoblinAttackState(Enemy enemy, GoblinAIController ai)
        : base(enemy, ai, attackCooldown: 0.8f)
    {
        this.controller = ai;
        this.agent = ai.Agent;
    }

    public override void OnEnter()
    {
        base.OnEnter();

        if (!enemy.IsServer) return;

        repositionTimer = 0f;
        isFeinting = false;

        originalSpeed = agent.speed;
        agent.speed *= 1.4f;
        agent.angularSpeed = 360f;

        Debug.Log($"[{enemy.name}] Goblin Attack ENTER");
    }

    public override void OnExit()
    {
        if (!enemy.IsServer) return;

        agent.speed = originalSpeed;

        if (controller.CurrentTarget != null)
        {
            CombatSlotManager.Instance?.RemoveFlanker(
                controller,
                controller.CurrentTarget.transform
            );
        }
    }

    public override void OnUpdate()
    {
        if (!enemy.IsServer) return;

        if (ai.ShouldFlee)
        {
            agent.speed = originalSpeed;
            ai.StateMachine.ChangeState(ai.FleeState);
            return;
        }

        if (ai.CurrentTarget == null) return;

    
        enemy.GetComponent<TargetingController>()?.ForceTarget(ai.CurrentTarget);

        repositionTimer += Time.deltaTime;

        // =========================
        // FEINT
        // =========================

        if (isFeinting)
        {
            UpdateFeint();
            return;
        }

        // =========================
        // MOVIMIENTO TÁCTICO
        // =========================

        if (repositionTimer >= repositionInterval)
        {
            repositionTimer = 0f;

            if (Random.value < feintChance)
            {
                StartFeint();
                return;
            }

            MoveToSlot();
        }

        RotateTowards(ai.CurrentTarget.transform.position);

       
        base.OnUpdate();
    }

    protected override void PerformAttack()
    {
        if (!enemy.IsServer) return;

        if (ai.CurrentTarget == null)
        {
            Debug.LogWarning("[Goblin] Attack sin target");
            return;
        }

      
        enemy.GetComponent<TargetingController>()?.ForceTarget(ai.CurrentTarget);

        Debug.Log($"[Goblin] ATAQUE -> {ai.CurrentTarget.name}");

        enemy.OnAttackPressed();
        enemy.OnAttackReleased();
    }

    // =========================
    // SLOT SYSTEM
    // =========================

    private void MoveToSlot()
    {
        Transform target = ai.CurrentTarget.transform;

        Vector3 slotPos = CombatSlotManager.Instance != null
            ? CombatSlotManager.Instance.GetFlankerSlotPosition(controller, target)
            : target.position;

        slotPos += CalculateSeparation();

        if (NavMesh.SamplePosition(slotPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    // =========================
    // SEPARACIÓN (SWARM FEEL)
    // =========================

    private Vector3 CalculateSeparation()
    {
        Collider[] hits = Physics.OverlapSphere(
            enemy.transform.position,
            separationRadius,
            LayerMask.GetMask("Enemy")
        );

        Vector3 separation = Vector3.zero;

        foreach (var hit in hits)
        {
            if (hit.gameObject == enemy.gameObject) continue;

            Vector3 diff = enemy.transform.position - hit.transform.position;
            float dist = diff.magnitude;

            if (dist > 0.01f)
                separation += diff.normalized / dist;
        }

        return separation * separationStrength;
    }

    // =========================
    // FEINT SYSTEM
    // =========================

    private void StartFeint()
    {
        isFeinting = true;
        feintTimer = 0f;

        Vector3 forward = (
            ai.CurrentTarget.transform.position
            - enemy.transform.position
        ).normalized;

        Vector3 pos = enemy.transform.position + forward * 1.5f;

        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }

        Debug.Log("[Goblin] FEINT");
    }

    private void UpdateFeint()
    {
        feintTimer += Time.deltaTime;

        if (feintTimer >= feintDuration)
        {
            isFeinting = false;
        }
    }

    // =========================
    // ROTACIÓN
    // =========================

    private void RotateTowards(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - enemy.transform.position).normalized;
        dir.y = 0;

        if (dir.sqrMagnitude < 0.01f) return;

        Quaternion rot = Quaternion.LookRotation(dir);

        enemy.transform.rotation = Quaternion.Slerp(
            enemy.transform.rotation,
            rot,
            Time.deltaTime * 10f
        );
    }
}