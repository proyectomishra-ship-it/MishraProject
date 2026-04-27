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
    private HashSet<EnemyGroupMember> allEnemies = new();

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
        if (member == null) return;

        if (allEnemies.Contains(member)) return;

        allEnemies.Add(member);

        if (member.GetEnemy() == null)
            Debug.LogError("[Coordinator] RegisterEnemy -> member sin Enemy!");

        Debug.Log($"[Coordinator] Registrado: {member.GetEnemy()?.name} ({member.Role})");
    }

    public void UnregisterEnemy(EnemyGroupMember member)
    {
        if (member == null) return;

        allEnemies.Remove(member);
    }

    // -------------------------
    // FORMACIÓN DE GRUPOS
    // -------------------------

    private void UpdateGroups()
    {
        //  LIMPIEZA GLOBAL 
        allEnemies.RemoveWhere(m =>
            m == null ||
            !m.IsAlive ||
            m.GetEnemy() == null);

        var ungrouped = allEnemies
            .Where(m =>
                m != null &&
                m.IsAlive &&
                m.CurrentGroup == null &&
                m.GetEnemy() != null)
            .ToList();

        foreach (var member in ungrouped)
        {
            if (member == null || member.GetEnemy() == null)
            {
                Debug.LogError("[Coordinator] Member inválido en ungrouped");
                continue;
            }

            if (member.CurrentGroup != null) continue;

            var memberEnemy = member.GetEnemy();

            var neighbors = allEnemies
                .Where(other =>
                    other != null &&
                    other != member &&
                    other.IsAlive &&
                    other.GetEnemy() != null &&
                    memberEnemy != null &&
                    Vector3.Distance(
                        memberEnemy.transform.position,
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
                {
                    if (neighbor == null || neighbor.GetEnemy() == null)
                    {
                        Debug.LogError("[Coordinator] Neighbor inválido al agregar a grupo");
                        continue;
                    }

                    newGroup.AddMember(neighbor);
                }
            }
        }

        var emptyGroups = groups
            .Where(g => g.Value == null || g.Value.IsEmpty)
            .Select(g => g.Key)
            .ToList();

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
    // COORDINACIÓN
    // -------------------------

    public Vector3 GetTacticalPosition(EnemyGroupMember member)
    {
        if (member == null || member.GetEnemy() == null)
            return Vector3.zero;

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
        if (group.HasTank && group.ActiveTank?.GetEnemy() != null)
        {
            Vector3 tankPos = group.ActiveTank.GetEnemy().transform.position;
            Vector3 dir = (tankPos - targetPos).normalized;
            return tankPos + dir * 5f;
        }

        return targetPos;
    }

    private Vector3 GetLeaderPosition(Vector3 targetPos)
    {
        if (Camera.main == null) return targetPos;

        return targetPos - Camera.main.transform.forward * 15f;
    }

    // -------------------------
    // GETTERS
    // -------------------------

    public EnemyGroup GetGroup(int id) =>
        groups.TryGetValue(id, out var group) ? group : null;

    public int GroupCount => groups.Count;

    public int TotalEnemies =>
        allEnemies.Count(m => m != null && m.IsAlive && m.GetEnemy() != null);
}