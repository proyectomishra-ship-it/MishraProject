using UnityEngine;

public class OrcArcherAIController : EnemyAIController
{
    [Header("Orc Archer")]
    [SerializeField] private float preferredCombatDistance = 12f;
    [SerializeField] private GameObject arrowPrefab;

    public float PreferredCombatDistance => preferredCombatDistance;
    public GameObject ArrowPrefab => arrowPrefab;

    protected override EnemyStateAttack CreateAttackState()
    {
        return new OrcArcherAttackState(GetComponent<Enemy>(), this);
    }
}