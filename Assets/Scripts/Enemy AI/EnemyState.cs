using UnityEngine;

public abstract class EnemyState
{
    protected Enemy enemy;
    protected EnemyAIController ai;

    public EnemyState(Enemy enemy, EnemyAIController ai)
    {
        this.enemy = enemy;
        this.ai = ai;
    }


    public virtual void OnEnter() { }


    public virtual void OnUpdate() { }

    public virtual void OnExit() { }
}