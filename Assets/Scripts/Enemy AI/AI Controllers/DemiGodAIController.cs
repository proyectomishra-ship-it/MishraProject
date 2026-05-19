using UnityEngine;
using Unity.Netcode;

public class DemiGodAIController : EnemyAIController
{
    [Header("Phase Thresholds")]
    [SerializeField]
    private float phase2Threshold = 0.6f;

    [SerializeField]
    private float phase3Threshold = 0.3f;

    [Header("Attacks")]
    [SerializeField]
    private GameObject spellPrefab;

    [SerializeField]
    private GameObject specialPrefab;

    [Header("Summon Orc")]
    [SerializeField]
    private GameObject orcPrefab;

    [SerializeField]
    private int phase2SummonCount = 2;

    [SerializeField]
    private float phase2SummonCooldown = 20f;

    [Header("Summon Goblin")]
    [SerializeField]
    private GameObject goblinPrefab;

    [SerializeField]
    private int phase3SummonCount = 4;

    [SerializeField]
    private float phase3SummonCooldown = 12f;

    [Header("Defense")]
    [SerializeField]
    private float tooCloseDistance = 3f;

    private int currentPhase = 1;

    private Enemy enemyComponent;

    public GameObject SpellPrefab => spellPrefab;

    public GameObject SpecialPrefab => specialPrefab;

    public GameObject OrcPrefab => orcPrefab;

    public GameObject GoblinPrefab => goblinPrefab;

    public float Phase2SummonCooldown =>
        phase2SummonCooldown;

    public float Phase3SummonCooldown =>
        phase3SummonCooldown;

    public float TooCloseDistance =>
        tooCloseDistance;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
            return;

        enemyComponent =
            GetComponent<Enemy>();

        FleeState = null;
    }

    protected override void Update()
    {
        if (!IsServer)
            return;

        CheckPhaseTransition();

        base.Update();
    }

    protected override EnemyStateAttack CreateAttackState()
    {
        return new DemiGodPhase1State(
            GetComponent<Enemy>(),
            this);
    }

    private void CheckPhaseTransition()
    {
        if (enemyComponent == null)
            return;

        ResourceController resources =
            enemyComponent.GetResourceController();

        if (resources == null)
            return;

        float maxHealth =
            enemyComponent.GetStats().MaxHealth.Value;

        if (maxHealth <= 0f)
            return;

        float healthPercent =
            resources.CurrentHealth / maxHealth;

        if (currentPhase == 1 &&
            healthPercent <= phase2Threshold)
        {
            EnterPhase2();
        }
        else if (currentPhase == 2 &&
                 healthPercent <= phase3Threshold)
        {
            EnterPhase3();
        }
    }

    private void EnterPhase2()
    {
        currentPhase = 2;

        SummonAllies(
            orcPrefab,
            phase2SummonCount,
            5f);

        AttackState =
            new DemiGodPhase2State(
                enemyComponent,
                this);

        if (StateMachine.CurrentState is EnemyStateAttack)
        {
            StateMachine.ChangeState(AttackState);
        }

        Debug.Log("[DemiGod] Enter Phase 2");
    }

    private void EnterPhase3()
    {
        currentPhase = 3;

        SummonAllies(
            goblinPrefab,
            phase3SummonCount,
            4f);

        AttackState =
            new DemiGodPhase3State(
                enemyComponent,
                this);

        if (StateMachine.CurrentState is EnemyStateAttack)
        {
            StateMachine.ChangeState(AttackState);
        }

        Debug.Log("[DemiGod] Enter Phase 3");
    }

    public void SummonAllies(
        GameObject prefab,
        int count,
        float spawnRadius)
    {
        if (!IsServer || prefab == null)
            return;

        for (int i = 0; i < count; i++)
        {
            float angle =
                i * (360f / count) * Mathf.Deg2Rad;

            Vector3 offset =
                new Vector3(
                    Mathf.Sin(angle) * spawnRadius,
                    0f,
                    Mathf.Cos(angle) * spawnRadius);

            Vector3 spawnPosition =
                transform.position + offset;

            GameObject instance =
                Instantiate(
                    prefab,
                    spawnPosition,
                    Quaternion.identity);

            if (instance.TryGetComponent(
                out NetworkObject networkObject))
            {
                networkObject.Spawn();
            }
        }
    }
}