using UnityEngine;

public class EnemyStateMachine
{
    public EnemyState CurrentState { get; private set; }

    public void ChangeState(EnemyState newState)
    {
        if (CurrentState == newState)
            return;

        CurrentState?.OnExit();

        CurrentState = newState;

        CurrentState.OnEnter();

        Debug.Log($"[StateMachine] → {newState.GetType().Name}");
    
    }

    public void Update()
    {
        CurrentState?.OnUpdate();
    }
}