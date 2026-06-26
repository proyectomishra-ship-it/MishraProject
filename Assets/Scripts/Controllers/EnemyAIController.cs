using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class EnemyAIController : NetworkBehaviour
{
    private Enemy enemy;

    [Header("Perception")]
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private float fieldOfViewAngle = 120f;
    [SerializeField] private float alertRadius = 15f;

    [Header("Chase")]
    [SerializeField] private float lostTargetTime = 3f;

    [Header("Idle")]
    [SerializeField] private float idleDuration = 2f;

    [Header("Patrol - Waypoints")]
    [SerializeField] private Transform[] waypoints;

    [Header("Patrol - Random")]
    [SerializeField] private bool useRandomPatrol = false;
    [SerializeField] private float randomPatrolRadius = 10f;

    [Header("Flee")]
    [SerializeField] private float fleeHealthThreshold = 0.25f;
    [SerializeField] private float fleeDistance = 15f;
    [SerializeField] private bool canFlee = true;

    // Componentes
    public NavMeshAgent Agent { get; private set; }
    public EnemyPerceptionSystem Perception { get; private set; }
    public EnemyStateMachine StateMachine { get; private set; }

    // Estados
    public EnemyStateIdle IdleState { get; private set; }
    public EnemyStatePatrol PatrolState { get; private set; }
    public EnemyStateChase ChaseState { get; private set; }
    public EnemyStateAttack AttackState { get; protected set; }
    public EnemyStateFlee FleeState { get; protected set; }

    public Character CurrentTarget { get; private set; }
    public bool IsAlerted { get; private set; }

    private int currentWaypointIndex = 0;
    private Vector3 randomPatrolOrigin;

    public bool HasPatrolPoints =>
        (waypoints != null && waypoints.Length > 0) || useRandomPatrol;

    public bool ShouldFlee
    {
        get
        {
            if (!canFlee) return false;

            var stats = enemy.GetStats();

            return stats != null
                && stats.MaxHealth.Value > 0
                && stats.CurrentHealth / stats.MaxHealth.Value <= fleeHealthThreshold;
        }
    }

    public void Initialize(Enemy enemy)
    {
        this.enemy = enemy;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        Agent = GetComponent<NavMeshAgent>();

        if (Agent == null)
        {
            Debug.LogError($"[EnemyAIController] Falta NavMeshAgent en {enemy.name}");
            return;
        }

        StartCoroutine(InitializeAI());
    }

    private IEnumerator InitializeAI()
    {
        yield return null;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            Debug.LogError($"[{enemy.name}] No NavMesh debajo del enemigo");
            yield break;
        }

        Agent.Warp(hit.position);

        if (!Agent.isOnNavMesh)
        {
            Debug.LogError($"[{enemy.name}] Warp falló (no está en NavMesh)");
            yield break;
        }

        randomPatrolOrigin = transform.position;

        Perception = new EnemyPerceptionSystem(
            enemy,
            detectionRadius,
            fieldOfViewAngle,
            alertRadius);

        StateMachine = new EnemyStateMachine();

        IdleState = new EnemyStateIdle(enemy, this, idleDuration);
        PatrolState = new EnemyStatePatrol(enemy, this);
        ChaseState = new EnemyStateChase(enemy, this, lostTargetTime);
        FleeState = new EnemyStateFlee(enemy, this, fleeDistance);

        AttackState = CreateAttackState();

        if (AttackState == null)
            Debug.LogError($"[EnemyAIController] AttackState null en {enemy.name}");

        StateMachine.ChangeState(IdleState);
    }

    protected virtual EnemyStateAttack CreateAttackState()
    {
        return null;
    }

    protected virtual void Update()
    {
        if (!IsServer) return;
        StateMachine?.Update();
    }

    // -------------------------
    // FIX CLAVE: reactivación de patrol desde idle
    // -------------------------

    public void RequestPatrolFromIdle()
    {
        if (!HasPatrolPoints) return;
        StateMachine.ChangeState(PatrolState);
    }

    // -------------------------
    // WAYPOINTS
    // -------------------------

    public Vector3 GetCurrentWaypoint()
    {
        if (useRandomPatrol)
            return GetRandomNavMeshPoint();

        if (waypoints != null && waypoints.Length > 0)
            return waypoints[currentWaypointIndex].position;

        return transform.position;
    }

    public void AdvanceWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
    }

    private Vector3 GetRandomNavMeshPoint()
    {
        Vector2 randomCircle = Random.insideUnitCircle * randomPatrolRadius;

        Vector3 randomPoint = randomPatrolOrigin +
                              new Vector3(randomCircle.x, 0f, randomCircle.y);

        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, randomPatrolRadius, NavMesh.AllAreas))
            return hit.position;

        return transform.position;
    }

    public void SetTarget(Character target) => CurrentTarget = target;
    public void SetAlerted(bool alerted) => IsAlerted = alerted;

    private void OnDrawGizmosSelected()
    {
        if (Perception == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, Perception.DetectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, Perception.AlertRadius);

        Gizmos.color = Color.cyan;

        Vector3 fovLeft =
            Quaternion.Euler(0, -Perception.FieldOfViewAngle / 2f, 0) *
            transform.forward * Perception.DetectionRadius;

        Vector3 fovRight =
            Quaternion.Euler(0, Perception.FieldOfViewAngle / 2f, 0) *
            transform.forward * Perception.DetectionRadius;

        Gizmos.DrawLine(transform.position, transform.position + fovLeft);
        Gizmos.DrawLine(transform.position, transform.position + fovRight);

        if (!canFlee) return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, fleeDistance);
    }
}