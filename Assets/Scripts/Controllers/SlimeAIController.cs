using UnityEngine;

public class SlimeAIController : EnemyAIController
{
    [Header("Slime Combat")]
    [SerializeField] private float preferredCombatDistance = 12f;
    [SerializeField] private GameObject projectilePrefab;

    public float PreferredCombatDistance => preferredCombatDistance;
    public GameObject ProjectilePrefab => projectilePrefab;

    protected override EnemyStateAttack CreateAttackState()
    {
        Enemy enemy = GetComponent<Enemy>();

        if (enemy == null)
        {
            Debug.LogError(
                $"[SlimeAIController] No se encontró Enemy en {gameObject.name}.",
                this);

            return null;
        }

        return new SlimeAttackState(enemy, this);
    }
}