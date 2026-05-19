using UnityEngine;
using UnityEngine.AI;

public class EnemyStateFlee : EnemyState
{
    private float fleeDistance;

    private float updateDestinationInterval =
        0.5f;

    private float updateTimer;

    private float originalSpeed;

    public EnemyStateFlee(
        Enemy enemy,
        EnemyAIController ai,
        float fleeDistance)
        : base(enemy, ai)
    {
        this.fleeDistance = fleeDistance;
    }

    // =========================
    // ENTER
    // =========================

    public override void OnEnter()
    {
        if (!enemy.IsServer)
            return;

        updateTimer =
            updateDestinationInterval;

        if (ai.Agent != null)
        {
            originalSpeed =
                ai.Agent.speed;

            ai.Agent.speed *= 1.3f;
        }

        Debug.Log(
            $"[{enemy.name}] Flee");

        UpdateFleeDestination();
    }

    // =========================
    // UPDATE
    // =========================

    public override void OnUpdate()
    {
        if (!enemy.IsServer)
            return;

        if (!ai.ShouldFlee)
        {
            ai.StateMachine.ChangeState(
                ai.IdleState);

            return;
        }

        updateTimer += Time.deltaTime;

        if (updateTimer <
            updateDestinationInterval)
            return;

        updateTimer = 0f;

        UpdateFleeDestination();
    }

    // =========================
    // EXIT
    // =========================

    public override void OnExit()
    {
        if (ai.Agent != null)
        {
            ai.Agent.speed =
                originalSpeed;
        }
    }

    // =========================
    // FLEE DESTINATION
    // =========================

    private void UpdateFleeDestination()
    {
        Character nearestPlayer =
            FindNearestPlayer();

        if (nearestPlayer == null)
            return;

        Vector3 direction =
            (
                enemy.transform.position -
                nearestPlayer.transform.position
            ).normalized;

        Vector3 fleeTarget =
            enemy.transform.position +
            direction * fleeDistance;

        if (NavMesh.SamplePosition(
            fleeTarget,
            out NavMeshHit hit,
            fleeDistance,
            NavMesh.AllAreas))
        {
            ai.Agent.SetDestination(
                hit.position);
        }
    }

    // =========================
    // FIND PLAYER
    // =========================

    private Character FindNearestPlayer()
    {
        if (ai.CurrentTarget != null)
            return ai.CurrentTarget;

        Collider[] hits =
            Physics.OverlapSphere(
                enemy.transform.position,
                30f);

        Character nearest = null;

        float nearestDistance =
            float.MaxValue;

        foreach (Collider hit in hits)
        {
            Player player =
                hit.GetComponent<Player>();

            if (player == null)
                continue;

            float distance =
                Vector3.Distance(
                    enemy.transform.position,
                    player.transform.position);

            if (distance >= nearestDistance)
                continue;

            nearest = player;

            nearestDistance = distance;
        }

        return nearest;
    }
}