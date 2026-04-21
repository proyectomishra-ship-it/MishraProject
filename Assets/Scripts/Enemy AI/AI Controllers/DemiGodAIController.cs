using UnityEngine;
using Unity.Netcode;

public class DemiGodAIController : EnemyAIController
{
    [Header("Phase Thresholds")]
    [SerializeField] private float phase2Threshold = 0.6f;
    [SerializeField] private float phase3Threshold = 0.3f;

    [Header("Attacks")]
    [SerializeField] private GameObject spellPrefab;
    [SerializeField] private GameObject specialPrefab;

    [Header("Summon Orc")]
    [SerializeField] private GameObject orcPrefab;
    [SerializeField] private int phase2SummonCount = 2;
    [SerializeField] private float phase2SummonCooldown = 20f;
    public GameObject OrcPrefab => orcPrefab;

   

    [Header("Summon Goblin")]
    [SerializeField] private GameObject goblinPrefab;
    [SerializeField] private int phase3SummonCount = 4;
    [SerializeField] private float phase3SummonCooldown = 12f;
    public GameObject GoblinPrefab => goblinPrefab;

    [Header("Defense")]
    
    [SerializeField] private float tooCloseDistance = 3f;

    public GameObject SpellPrefab => spellPrefab;
    public GameObject SpecialPrefab => specialPrefab;
    public float Phase2SummonCooldown => phase2SummonCooldown;
    public float Phase3SummonCooldown => phase3SummonCooldown;
    public float TooCloseDistance => tooCloseDistance;

    private int currentPhase = 1;
    private Enemy demiGodEnemy;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;

        demiGodEnemy = GetComponent<Enemy>();

     
        FleeState = null;
    }

    private void Update()
    {
        if (!IsServer) return;
        CheckPhaseTransition();
        StateMachine?.Update();
    }

    protected override EnemyStateAttack CreateAttackState()
    {
        return new DemiGodPhase1State(GetComponent<Enemy>(), this);
    }

    private void CheckPhaseTransition()
    {
        if (demiGodEnemy == null) return;
        var rc = demiGodEnemy.GetResourceController();
        if (rc == null) return;

        float hp = rc.CurrentHealth / demiGodEnemy.GetStats().MaxHealth.Value;

        if (currentPhase == 1 && hp <= phase2Threshold)
        {
            currentPhase = 2;
            EnterPhase2();
        }
        else if (currentPhase == 2 && hp <= phase3Threshold)
        {
            currentPhase = 3;
            EnterPhase3();
        }
    }

    private void EnterPhase2()
    {
        Debug.Log("[DemiGod] ¡Fase 2 — Magia + Aliados!");
        SummonAllies(orcPrefab, phase2SummonCount, spawnRadius: 5f);
        AttackState = new DemiGodPhase2State(demiGodEnemy, this);

        if (StateMachine.CurrentState is EnemyStateAttack)
            StateMachine.ChangeState(AttackState);
    }

    private void EnterPhase3()
    {
        Debug.Log("[DemiGod] ¡Fase 3 — Berserker Mágico!");
        SummonAllies(goblinPrefab, phase3SummonCount, spawnRadius: 4f);
        AttackState = new DemiGodPhase3State(demiGodEnemy, this);

        if (StateMachine.CurrentState is EnemyStateAttack)
            StateMachine.ChangeState(AttackState);
    }

    public void SummonAllies(GameObject prefab, int count, float spawnRadius)
    {
        if (!IsServer || prefab == null) return;

        for (int i = 0; i < count; i++)
        {
            float angle = i * (360f / count) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Sin(angle) * spawnRadius,
                0f,
                Mathf.Cos(angle) * spawnRadius);

            Vector3 spawnPos = transform.position + offset;
            var obj = Instantiate(prefab, spawnPos, Quaternion.identity);

            if (obj.TryGetComponent<NetworkObject>(out var netObj))
                netObj.Spawn();
        }

        Debug.Log($"[DemiGod] Convocó {count} aliados.");
    }
}