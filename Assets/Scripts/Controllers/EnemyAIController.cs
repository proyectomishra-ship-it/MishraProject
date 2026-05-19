using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class EnemyAIController : NetworkBehaviour
{
    private Enemy enemy;

    [Header("Perception")]
    [SerializeField]
    private float detectionRadius = 10f;

    [SerializeField]
    private float fieldOfViewAngle = 120f;

    [SerializeField]
    private float alertRadius = 15f;

    [Header("Chase")]
    [SerializeField]
    private float lostTargetTime = 3f;

    [Header("Idle")]
    [SerializeField]
    private float idleDuration = 2f;

    [Header("Patrol - Waypoints")]
    [SerializeField]
    private Transform[] waypoints;

    [Header("Patrol - Random")]
    [SerializeField]
    private bool useRandomPatrol = false;

    [SerializeField]
    private float randomPatrolRadius = 10f;

    [Header("Patrol Config")]
    [SerializeField]
    private int maxPatrolMoves = 3;

    [Header("Flee")]
    [SerializeField]
    private float fleeHealthThreshold = 0.25f;

    [SerializeField]
    private float fleeDistance = 15f;

    [SerializeField]
    private bool canFlee = true;

    public NavMeshAgent Agent { get; private set; }

    public EnemyPerceptionSystem Perception { get; private set; }

    public EnemyStateMachine StateMachine { get; private set; }

    public EnemyStateIdle IdleState { get; protected set; }

    public EnemyStatePatrol PatrolState { get; protected set; }

    public EnemyStateChase ChaseState { get; protected set; }

    public EnemyStateAttack AttackState { get; protected set; }

    public EnemyStateFlee FleeState { get; protected set; }

    public Character CurrentTarget { get; private set; }

    public bool IsAlerted { get; private set; }

    private int currentWaypointIndex;

    private Vector3 randomPatrolOrigin;

    public bool HasPatrolPoints =>
        (waypoints != null && waypoints.Length > 0) ||
        useRandomPatrol;

    public bool ShouldFlee
    {
        get
        {
            if (!canFlee)
                return false;

            ResourceController resources =
                enemy.GetResourceController();

            if (resources == null)
                return false;

            float maxHealth =
                enemy.GetStats().MaxHealth.Value;

            if (maxHealth <= 0f)
                return false;

            return resources.CurrentHealth / maxHealth
                   <= fleeHealthThreshold;
        }
    }

    // =========================
    // INITIALIZE
    // =========================

    public void Initialize(Enemy enemy)
    {
        this.enemy = enemy;
    }

    // =========================
    // NETWORK SPAWN
    // =========================

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        if (enemy == null)
        {
            enemy = GetComponent<Enemy>();
        }

        Agent =
            GetComponent<NavMeshAgent>();

        if (Agent == null)
        {
            Debug.LogError(
                $"[EnemyAIController] Missing NavMeshAgent on {name}");

            return;
        }

        Agent.speed =
            enemy.GetStats().Speed.Value;

        randomPatrolOrigin =
            transform.position;

        // =========================
        // PERCEPTION
        // =========================

        Perception =
            new EnemyPerceptionSystem(
                enemy,
                detectionRadius,
                fieldOfViewAngle,
                alertRadius);

        // =========================
        // STATE MACHINE
        // =========================

        StateMachine =
            new EnemyStateMachine();

        IdleState =
            new EnemyStateIdle(
                enemy,
                this,
                idleDuration);

        PatrolState =
            new EnemyStatePatrol(
                enemy,
                this,
                maxPatrolMoves);

        ChaseState =
            new EnemyStateChase(
                enemy,
                this,
                lostTargetTime);

        FleeState =
            canFlee
                ? new EnemyStateFlee(
                    enemy,
                    this,
                    fleeDistance)
                : null;

        AttackState =
            CreateAttackState();

        // =========================
        // INITIAL STATE
        // =========================

        StateMachine.ChangeState(
            HasPatrolPoints
                ? PatrolState
                : IdleState);

        Debug.Log(
            $"[EnemyAIController] Initialized {name}");
    }

    // =========================
    // STATE FACTORY
    // =========================

    protected virtual EnemyStateAttack CreateAttackState()
    {
        return null;
    }

    // =========================
    // UPDATE
    // =========================

    protected virtual void Update()
    {
        if (!IsServer)
            return;

        StateMachine?.Update();
    }

    // =========================
    // PATROL
    // =========================

    public Vector3 GetCurrentWaypoint()
    {
        if (useRandomPatrol)
            return GetRandomNavMeshPoint();

        if (waypoints != null &&
            waypoints.Length > 0)
        {
            return waypoints[currentWaypointIndex]
                .position;
        }

        return transform.position;
    }

    public void AdvanceWaypoint()
    {
        if (waypoints == null ||
            waypoints.Length == 0)
            return;

        currentWaypointIndex =
            (currentWaypointIndex + 1)
            % waypoints.Length;
    }

    private Vector3 GetRandomNavMeshPoint()
    {
        Vector2 randomCircle =
            Random.insideUnitCircle *
            randomPatrolRadius;

        Vector3 randomPoint =
            randomPatrolOrigin +
            new Vector3(
                randomCircle.x,
                0f,
                randomCircle.y);

        if (NavMesh.SamplePosition(
            randomPoint,
            out NavMeshHit hit,
            randomPatrolRadius,
            NavMesh.AllAreas))
        {
            return hit.position;
        }

        return transform.position;
    }

    // =========================
    // TARGET
    // =========================

    public void SetTarget(Character target)
    {
        CurrentTarget = target;

        enemy.GetTargetingController()
            ?.ForceTarget(target);
    }

    public void ClearTarget()
    {
        CurrentTarget = null;

        enemy.GetTargetingController()
            ?.ForceTarget(null);
    }

    // =========================
    // ALERT
    // =========================

    public void SetAlerted(bool alerted)
    {
        IsAlerted = alerted;
    }

    // =========================
    // DEBUG
    // =========================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            detectionRadius);

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            alertRadius);

        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(
            transform.position,
            randomPatrolRadius);
    }
}