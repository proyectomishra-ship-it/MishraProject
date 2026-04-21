using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

public class EnemyGroupCoordinator : NetworkBehaviour
{
    public static EnemyGroupCoordinator Instance { get; private set; }

    [Header("Group Formation")]
    
    [SerializeField] private float groupFormationRadius = 20f;
  
    [SerializeField] private float groupUpdateInterval = 3f;

    private Dictionary<int, EnemyGroup> groups = new();
    private List<EnemyGroupMember> allEnemies = new();
    private int nextGroupId = 0;
    private float updateTimer;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if (!IsServer) return;

        updateTimer += Time.deltaTime;
        if (updateTimer >= groupUpdateInterval)
        {
            updateTimer = 0f;
            UpdateGroups();
        }
    }

    // -------------------------
    // REGISTRO
    // -------------------------

    public void RegisterEnemy(EnemyGroupMember member)
    {
        if (allEnemies.Contains(member)) return;
        allEnemies.Add(member);
        Debug.Log($"[Coordinator] Registrado: {member.GetEnemy().name} ({member.Role})");
    }

    public void UnregisterEnemy(EnemyGroupMember member)
    {
        allEnemies.Remove(member);
    }

    // -------------------------
    // FORMACIÓN DE GRUPOS
    // -------------------------

 
    private void UpdateGroups()
    {
        var ungrouped = allEnemies
            .Where(m => m.IsAlive && m.CurrentGroup == null)
            .ToList();

        foreach (var member in ungrouped)
        {
            if (member.CurrentGroup != null) continue;

            
            var neighbors = allEnemies
                .Where(other =>
                    other != member &&
                    other.IsAlive &&
                    Vector3.Distance(
                        member.GetEnemy().transform.position,
                        other.GetEnemy().transform.position)
                    <= groupFormationRadius)
                .ToList();

            if (neighbors.Count == 0) continue;

         
            var existingGroup = neighbors
                .Where(n => n.CurrentGroup != null)
                .Select(n => n.CurrentGroup)
                .FirstOrDefault();

            if (existingGroup != null)
            {
                existingGroup.AddMember(member);
            }
            else
            {
            
                var newGroup = CreateGroup();
                newGroup.AddMember(member);

                foreach (var neighbor in neighbors.Where(n => n.CurrentGroup == null))
                    newGroup.AddMember(neighbor);
            }
        }

        var emptyGroups = groups.Where(g => g.Value.IsEmpty).Select(g => g.Key).ToList();
        foreach (var id in emptyGroups)
            groups.Remove(id);
    }

    private EnemyGroup CreateGroup()
    {
        var group = new EnemyGroup(nextGroupId++);
        groups[group.GroupId] = group;
        Debug.Log($"[Coordinator] Grupo {group.GroupId} creado.");
        return group;
    }

    // -------------------------
    // COORDINACIÓN DE ROLES
    // -------------------------


    public Vector3 GetTacticalPosition(EnemyGroupMember member)
    {
        var group = member.CurrentGroup;
        if (group == null) return member.GetEnemy().transform.position;

        Character target = group.GetSharedTarget();
        if (target == null) return member.GetEnemy().transform.position;

        Vector3 targetPos = target.transform.position;
        Vector3 enemyPos = member.GetEnemy().transform.position;

        return member.Role switch
        {
          
            EnemyRole.Tank => targetPos,

           
            EnemyRole.Ranged => GetRangedPosition(group, targetPos),

           
            EnemyRole.Flanker => CombatSlotManager.Instance != null
                ? CombatSlotManager.Instance.GetFlankerSlotPosition(
                    member.GetAIController() as GoblinAIController,
                    target.transform)
                : enemyPos,

           
            EnemyRole.Leader => GetLeaderPosition(targetPos),

            _ => enemyPos
        };
    }


    private Vector3 GetRangedPosition(EnemyGroup group, Vector3 targetPos)
    {
        if (group.HasTank)
        {
            Vector3 tankPos = group.ActiveTank.GetEnemy().transform.position;
            Vector3 dirFromTarget = (tankPos - targetPos).normalized;
         
            return tankPos + dirFromTarget * 5f;
        }

        return targetPos + (targetPos - targetPos).normalized * 10f;
    }


    private Vector3 GetLeaderPosition(Vector3 targetPos)
    {
        
        return targetPos - Camera.main.transform.forward * 15f;
    }

    // -------------------------
    // GETTERS
    // -------------------------

    public EnemyGroup GetGroup(int id) =>
        groups.TryGetValue(id, out var group) ? group : null;

    public int GroupCount => groups.Count;
    public int TotalEnemies => allEnemies.Count(m => m.IsAlive);

    // -------------------------
    // GIZMOS
    // -------------------------

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        foreach (var group in groups.Values)
        {
            if (group.IsEmpty) continue;

          
            var members = group.Members.Where(m => m.IsAlive).ToList();
            for (int i = 0; i < members.Count; i++)
            {
                for (int j = i + 1; j < members.Count; j++)
                {
                    Gizmos.color = group.HasTank ? Color.green : Color.red;
                    Gizmos.DrawLine(
                        members[i].GetEnemy().transform.position,
                        members[j].GetEnemy().transform.position);
                }
            }

            
            if (group.HasTank)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(
                    group.ActiveTank.GetEnemy().transform.position, 1f);
            }
        }
    }
}