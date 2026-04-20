using UnityEngine;

public interface IEnemyStrategy
{
    void OnEnter(Enemy enemy, EnemyAIController ai);
    void OnUpdate(Enemy enemy, EnemyAIController ai);
    void OnExit(Enemy enemy, EnemyAIController ai);
}