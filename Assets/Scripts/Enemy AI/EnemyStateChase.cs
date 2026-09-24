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
          

            ai.StateMachine.ChangeState(ai.FleeState);
            return;
        }

        // =====================================================
        // VALIDACIÓN TARGET
        // =====================================================

        if (ai.CurrentTarget == null)
        {
           

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

         

            // =================================================
            // ATTACK
            // =================================================

            if (inRange)
            {
                

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

           
        }

        lostTargetTimer += Time.deltaTime;

       

        if (lostTargetTimer >= lostTargetTime)
        {
            

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

     
    }
}