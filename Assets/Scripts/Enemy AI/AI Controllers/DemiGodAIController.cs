using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class DemiGodAIController : EnemyAIController
{
    [Header("Phase Thresholds")]
    [SerializeField] private float phase2Threshold = 0.6f;
    [SerializeField] private float phase3Threshold = 0.3f;

    [Header("Attacks")]
    [Tooltip("Proyectil del hechizo normal (Demigod's Fireball).")]
    [SerializeField] private GameObject spellPrefab;

    [Tooltip("Proyectil rápido de la Orbe (Special Attack). Si está vacío usa el hechizo normal.")]
    [SerializeField] private GameObject specialPrefab;

    [Header("Summon Orc")]
    [SerializeField] private GameObject orcPrefab;
    [Tooltip("Orcos que invoca al entrar en la fase 2.")]
    [SerializeField] private int phase2SummonCount = 2;
    [SerializeField] private float phase2SummonCooldown = 20f; // heredado: ya no se usa (ver Combat > Moves > Summon)
    public GameObject OrcPrefab => orcPrefab;

    [Header("Summon Goblin")]
    [SerializeField] private GameObject goblinPrefab;
    [Tooltip("Goblins que invoca al entrar en la fase 3.")]
    [SerializeField] private int phase3SummonCount = 4;
    [SerializeField] private float phase3SummonCooldown = 12f; // heredado: ya no se usa (ver Combat > Moves > Summon)
    public GameObject GoblinPrefab => goblinPrefab;

    [Header("Defense")]
    [SerializeField] private float tooCloseDistance = 3f; // heredado: ver Combat > Panic Distance

    [Header("Combat")]
    [Tooltip("Alternancia distancia <-> cuerpo a cuerpo, distancias, tiempos y lista de movimientos.")]
    [SerializeField] private DemiGodCombatSettings combat = new DemiGodCombatSettings();

    public GameObject SpellPrefab => spellPrefab;
    public GameObject SpecialPrefab => specialPrefab;
    public float Phase2SummonCooldown => phase2SummonCooldown;
    public float Phase3SummonCooldown => phase3SummonCooldown;
    public float TooCloseDistance => tooCloseDistance;
    public DemiGodCombatSettings Combat => combat;
    public int CurrentPhase => currentPhase;

    private int currentPhase = 1;
    private Enemy demiGodEnemy;
    private DemiGodCombatState combatState;

    // Aliados invocados que siguen vivos (para respetar el tope).
    private readonly List<Enemy> summoned = new List<Enemy>();

    /// <summary>
    /// El boss nunca huye. Antes se intentaba con FleeState = null en OnNetworkSpawn, pero la base
    /// vuelve a crear ese estado un frame después, así que con poca vida terminaba corriendo de espaldas.
    /// </summary>
    public override bool ShouldFlee => false;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer) return;

        demiGodEnemy = GetComponent<Enemy>();
    }

    protected override void Update()
    {
        if (!IsServer) return;
        CheckPhaseTransition();
        EngageFromDistance();
        StateMachine?.Update();
    }

    private void EngageFromDistance()
    {
        if (StateMachine == null || AttackState == null || ChaseState == null)
            return;

        if (StateMachine.CurrentState != ChaseState || CurrentTarget == null)
            return;

        Vector3 a = transform.position;
        Vector3 b = CurrentTarget.transform.position;
        a.y = 0f;
        b.y = 0f;

        if (Vector3.Distance(a, b) <= combat.engageDistance)
        {
            Debug.Log("[DemiGod] Entra en combate a distancia");
            StateMachine.ChangeState(AttackState);
        }
    }

    protected override EnemyStateAttack CreateAttackState()
    {
        combatState = new DemiGodCombatState(GetComponent<Enemy>(), this);
        return combatState;
    }

    // =========================================================
    // FASES
    // =========================================================

    private void CheckPhaseTransition()
    {
        var stats = demiGodEnemy?.GetStats();

        if (stats == null)
            return;

        float healthPercent =
            stats.CurrentHealth / stats.MaxHealth.Value;

        if (currentPhase == 1 &&
            healthPercent <= phase2Threshold)
        {
            currentPhase = 2;
            EnterPhase2();
        }
        else if (currentPhase == 2 &&
                 healthPercent <= phase3Threshold)
        {
            currentPhase = 3;
            EnterPhase3();
        }
    }

    // Las fases ya no cambian de estado: el mismo DemiGodCombatState lee la fase actual
    // y habilita más movimientos y tiempos más cortos. Al entrar abre con un golpe de área.

    private void EnterPhase2()
    {
        Debug.Log("[DemiGod] pasa a fase 2: se suman Volley, Meteors y Summon");
        SummonAllies(orcPrefab, phase2SummonCount, spawnRadius: 5f);
        combatState?.OnPhaseChanged();
    }

    private void EnterPhase3()
    {
        Debug.Log("[DemiGod] pasa a Fase 3: ritmo mas rapido + invoca orcos y goblins");
        SummonAllies(goblinPrefab, phase3SummonCount, spawnRadius: 4f);
        combatState?.OnPhaseChanged();
    }

    // =========================================================
    // ALIADOS (con tope)
    // =========================================================

    public int AliveAdds
    {
        get
        {
            summoned.RemoveAll(e =>
                e == null ||
                !e.IsSpawned ||
                e.GetStats() == null ||
                e.GetStats().CurrentHealth <= 0f);

            return summoned.Count;
        }
    }

    /// <summary>¿Queda lugar para invocar? (tope en Combat > Max Alive Adds)</summary>
    public bool CanSummon => AliveAdds < combat.maxAliveAdds;

    /// <summary>Invocación del movimiento Summon: en la fase 3 suma un orco.</summary>
    public void SummonWave()
    {
        if (currentPhase >= 3)
            SummonAllies(orcPrefab, 1, 5f);

        SummonAllies(goblinPrefab, 2, 4f);
    }

    public void SummonAllies(GameObject prefab, int count, float spawnRadius)
    {
        if (!IsServer || prefab == null) return;

        // Nunca supera el tope de aliados vivos.
        count = Mathf.Min(count, Mathf.Max(0, combat.maxAliveAdds - AliveAdds));

        if (count <= 0)
        {
            Debug.Log("[DemiGod] Invocación omitida: tope de aliados alcanzado.");
            return;
        }

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

            if (obj.TryGetComponent<Enemy>(out var ally))
                summoned.Add(ally);
        }

        Debug.Log($"[DemiGod] Convoca {count} aliados.");
    }

    // =========================================================
    // AVISOS Y EFECTOS (se dibujan en todos los clientes)
    // =========================================================

    private static readonly Color ImpactColor = new Color(1f, 0.45f, 0.15f, 1f);

    /// <summary>Aviso circular en el suelo (Slam, Meteors).</summary>
    public void ShowCircle(Vector3 center, float radius, float duration)
    {
        if (IsServer)
            PlayCircleClientRpc(center, radius, duration);
    }

    /// <summary>Aviso en franja en el suelo (Strike, Orb, Charge).</summary>
    public void ShowLane(Vector3 origin, Vector3 direction, float length, float width, float duration)
    {
        if (IsServer)
            PlayLaneClientRpc(origin, direction, length, width, duration);
    }

    /// <summary>Onda de choque en el momento del impacto.</summary>
    public void PlayImpact(Vector3 center, float radius)
    {
        if (IsServer)
            PlayImpactClientRpc(center, radius);
    }

    [ClientRpc]
    private void PlayCircleClientRpc(Vector3 center, float radius, float duration)
    {
        BossTelegraphVfx.PlayCircle(center, radius, duration);
    }

    [ClientRpc]
    private void PlayLaneClientRpc(Vector3 origin, Vector3 direction, float length, float width, float duration)
    {
        BossTelegraphVfx.PlayLane(origin, direction, length, width, duration);
    }

    [ClientRpc]
    private void PlayImpactClientRpc(Vector3 center, float radius)
    {
        SpecialAbilityVfx.PlayShockwave(center, radius, ImpactColor);
    }
}
