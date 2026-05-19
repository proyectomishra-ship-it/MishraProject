using UnityEngine;

public class EnemyStateMachine
{
    public EnemyState CurrentState
    {
        get;
        private set;
    }

    // =========================
    // CHANGE STATE
    // =========================

    public void ChangeState(
        EnemyState newState)
    {
        if (newState == null)
        {
            Debug.LogError(
                "[StateMachine] newState NULL");

            return;
        }

        if (CurrentState == newState)
            return;

        CurrentState?.OnExit();

        CurrentState = newState;

        CurrentState.OnEnter();

        Debug.Log(
            $"[StateMachine] -> {newState.GetType().Name}");
    }

    // =========================
    // UPDATE
    // =========================

    public void Update()
    {
        CurrentState?.OnUpdate();
    }
}