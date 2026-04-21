using UnityEngine;

public class AcolyteAIController : EnemyAIController
{
    [Header("Acolyte")]
    [SerializeField] private float preferredCombatDistance = 14f;
    [SerializeField] private GameObject spellPrefab;

    public float PreferredCombatDistance => preferredCombatDistance;
    public GameObject SpellPrefab => spellPrefab;

    protected override EnemyStateAttack CreateAttackState()
    {
        return new AcolyteAttackState(GetComponent<Enemy>(), this);
    }
}
