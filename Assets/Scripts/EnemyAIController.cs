using UnityEngine;

public class EnemyAIController : MonoBehaviour
{
    private Enemy enemy;
    private Character target;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    private void Update()
    {
        // Lógica IA
        // enemy.Move(...)
        // enemy.Attack(target)
    }
}