using UnityEngine;
using Unity.Netcode;

public class EnemyGroupMember : NetworkBehaviour
{
    public EnemyRole Role { get; private set; } = EnemyRole.Unassigned;
    public EnemyGroup CurrentGroup { get; private set; }
    public bool IsAlive { get; private set; } = true;

    private Enemy enemy;
    private EnemyAIController aiController;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        aiController = GetComponent<EnemyAIController>();

        if (enemy == null)
            Debug.LogError("[EnemyGroupMember] Enemy es NULL");

        if (aiController == null)
            Debug.LogError("[EnemyGroupMember] AIController es NULL");

        Role = DetectRole();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (EnemyGroupCoordinator.Instance == null)
        {
            Debug.LogError("[EnemyGroupMember] Coordinator NULL en spawn");
            return;
        }

        EnemyGroupCoordinator.Instance.RegisterEnemy(this);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        NotifyDeath();
    }

    public void AssignToGroup(EnemyGroup group)
    {
        CurrentGroup = group;
    }

    public void NotifyDeath()
    {
        if (!IsAlive) return;

        IsAlive = false;

        CurrentGroup?.OnMemberDied(this);

        if (EnemyGroupCoordinator.Instance != null)
            EnemyGroupCoordinator.Instance.UnregisterEnemy(this);
    }

    public void OnGroupTankDied()
    {
        if (!IsServer) return;
        if (aiController == null) return;

        switch (Role)
        {
            case EnemyRole.Ranged:
                Debug.Log($"[{enemy?.name}] Tank muerto — aumentando distancia.");
                break;

            case EnemyRole.Flanker:
                Debug.Log($"[{enemy?.name}] Tank muerto — flanqueo más agresivo.");
                break;
        }
    }

    private EnemyRole DetectRole()
    {
        return aiController switch
        {
            DemiGodAIController => EnemyRole.Leader,
            OrcAIController => EnemyRole.Tank,
            OrcArcherAIController or AcolyteAIController => EnemyRole.Ranged,
            GoblinAIController => EnemyRole.Flanker,
            _ => EnemyRole.Unassigned
        };
    }

    public Enemy GetEnemy() => enemy;
    public EnemyAIController GetAIController() => aiController;
}