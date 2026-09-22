using UnityEngine;
using UnityEngine.AI;

public class EnemyStateChase : EnemyState
{
    private float lostTargetTime;
    private float lostTargetTimer;
    private bool targetLost;

    public EnemyStateChase(
        Enemy enemy,
        EnemyAIController ai,
        float lostTargetTime)
        : base(enemy, ai)
    {
        this.lostTargetTime = lostTargetTime;
    }

    // =========================================================
    // ENTER
    // =========================================================

    public override void OnEnter()
    {
        lostTargetTimer = 0f;
        targetLost = false;

        if (ai.Agent != null)
            ai.Agent.isStopped = false;

        Debug.Log(
            $"[{enemy.name}][CHASE] ENTER -> " +
            $"Target={ai.CurrentTarget?.name ?? "NULL"} | " +
            $"EnemyPos={enemy.transform.position}"
        );
    }

    // =========================================================
    // UPDATE
    // =========================================================

    public override void OnUpdate()
    {
        // =====================================================
        // FLEE
        // =====================================================

        if (ai.ShouldFlee)
        {
            Debug.Log(
                $"[{enemy.name}][CHASE] ShouldFlee=true -> FLEE"
            );

            ai.StateMachine.ChangeState(ai.FleeState);
            return;
        }

        // =====================================================
        // VALIDACIÓN TARGET
        // =====================================================

        if (ai.CurrentTarget == null)
        {
            Debug.LogWarning(
                $"[{enemy.name}][CHASE] CurrentTarget=NULL"
            );

            HandleLostTarget();
            return;
        }

        // =====================================================
        // PERCEPTION
        // =====================================================

        Character detected =
            ai.Perception.DetectPlayer(ai.IsAlerted);

        if (detected != null)
        {
            targetLost = false;
            lostTargetTimer = 0f;

            // -------------------------------------------------
            // IMPORTANTE:
            // Mantener el target actual si sigue siendo válido.
            // -------------------------------------------------

            if (detected != ai.CurrentTarget)
            {
                Debug.Log(
                    $"[{enemy.name}][CHASE] Target actualizado: " +
                    $"{ai.CurrentTarget?.name ?? "NULL"} -> {detected.name}"
                );

                ai.SetTarget(detected);
            }

            // -------------------------------------------------
            // MOVIMIENTO
            // -------------------------------------------------

            if (ai.Agent != null &&
                ai.Agent.isOnNavMesh &&
                !ai.Agent.isStopped)
            {
                ai.Agent.SetDestination(
                    ai.CurrentTarget.transform.position
                );
            }

            // =================================================
            // RANGE CHECK
            // =================================================

            float fullDistance =
                Vector3.Distance(
                    enemy.transform.position,
                    ai.CurrentTarget.transform.position
                );

            // -------------------------------------------------
            // Distancia horizontal.
            //
            // Esto evita que una diferencia de altura entre
            // los pivots de los modelos impida entrar en melee.
            // -------------------------------------------------

            Vector3 enemyPosition =
                enemy.transform.position;

            Vector3 targetPosition =
                ai.CurrentTarget.transform.position;

            enemyPosition.y = 0f;
            targetPosition.y = 0f;

            float horizontalDistance =
                Vector3.Distance(
                    enemyPosition,
                    targetPosition
                );

            float attackRange =
                enemy.GetStats().AttackRange.Value;

            bool inRange =
                horizontalDistance <= attackRange;

            Debug.Log(
                $"[{enemy.name}][CHASE] RANGE CHECK | " +
                $"3DDistance={fullDistance:F2} | " +
                $"HorizontalDistance={horizontalDistance:F2} | " +
                $"AttackRange={attackRange:F2} | " +
                $"InRange={inRange} | " +
                $"EnemyPos={enemy.transform.position} | " +
                $"TargetPos={ai.CurrentTarget.transform.position}"
            );

            // =================================================
            // ATTACK
            // =================================================

            if (inRange)
            {
                Debug.Log(
                    $"[{enemy.name}][CHASE] " +
                    $"*** DENTRO DEL RANGO *** -> ATTACK | " +
                    $"HorizontalDistance={horizontalDistance:F2} <= " +
                    $"AttackRange={attackRange:F2}"
                );

                if (ai.AttackState == null)
                {
                    Debug.LogError(
                        $"[{enemy.name}][CHASE] ERROR: " +
                        $"AttackState es NULL."
                    );

                    return;
                }

                Debug.Log(
                    $"[{enemy.name}][CHASE] " +
                    $"CAMBIO DE ESTADO -> " +
                    $"{ai.AttackState.GetType().Name}"
                );

                ai.StateMachine.ChangeState(ai.AttackState);

                return;
            }
        }
        else
        {
            // =================================================
            // PERCEPCIÓN PERDIDA
            // =================================================

            Debug.Log(
                $"[{enemy.name}][CHASE] " +
                $"DetectPlayer() devolvió NULL | " +
                $"CurrentTarget={ai.CurrentTarget.name}"
            );

            HandleLostTarget();
        }
    }

    // =========================================================
    // LOST TARGET
    // =========================================================

    private void HandleLostTarget()
    {
        if (!targetLost)
        {
            targetLost = true;
            lostTargetTimer = 0f;

            if (ai.Agent != null)
                ai.Agent.ResetPath();

            Debug.Log(
                $"[{enemy.name}][CHASE] TARGET PERDIDO"
            );
        }

        lostTargetTimer += Time.deltaTime;

        Debug.Log(
            $"[{enemy.name}][CHASE] " +
            $"LostTargetTimer={lostTargetTimer:F2}/" +
            $"{lostTargetTime:F2}"
        );

        if (lostTargetTimer >= lostTargetTime)
        {
            Debug.Log(
                $"[{enemy.name}][CHASE] " +
                $"TARGET PERDIDO DURANTE {lostTargetTime:F1}s " +
                $"-> regresar a estado anterior"
            );

            ai.SetTarget(null);
            ai.SetAlerted(false);

            if (ai.HasPatrolPoints)
            {
                Debug.Log(
                    $"[{enemy.name}][CHASE] -> PATROL"
                );

                ai.StateMachine.ChangeState(ai.PatrolState);
            }
            else
            {
                Debug.Log(
                    $"[{enemy.name}][CHASE] -> IDLE"
                );

                ai.StateMachine.ChangeState(ai.IdleState);
            }
        }
    }

    // =========================================================
    // EXIT
    // =========================================================

    public override void OnExit()
    {
        if (ai.Agent != null)
            ai.Agent.ResetPath();

        Debug.Log(
            $"[{enemy.name}][CHASE] EXIT -> " +
            $"{ai.StateMachine.CurrentState?.GetType().Name ?? "NULL"}"
        );
    }
}