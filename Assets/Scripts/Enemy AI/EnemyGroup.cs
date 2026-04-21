using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemyGroup
{
    public int GroupId { get; private set; }
    public List<EnemyGroupMember> Members { get; private set; } = new();

    public EnemyGroupMember ActiveTank { get; private set; }
    public bool HasTank => ActiveTank != null && ActiveTank.IsAlive;

    public EnemyGroup(int id)
    {
        GroupId = id;
    }

    public void AddMember(EnemyGroupMember member)
    {
        if (Members.Contains(member)) return;
        Members.Add(member);
        member.AssignToGroup(this);

        
        if (member.Role == EnemyRole.Tank && ActiveTank == null)
            ActiveTank = member;

        Debug.Log($"[Group {GroupId}] +{member.GetEnemy().name} ({member.Role}) " +
                  $"— {Members.Count} miembros.");
    }

    public void RemoveMember(EnemyGroupMember member)
    {
        Members.Remove(member);

        if (member == ActiveTank)
        {
           
            ActiveTank = Members.FirstOrDefault(m =>
                m.Role == EnemyRole.Tank && m.IsAlive);
        }
    }

    public void OnMemberDied(EnemyGroupMember member)
    {
        RemoveMember(member);

        if (member.Role == EnemyRole.Tank)
        {
            
            foreach (var m in Members.Where(m => m.IsAlive))
                m.OnGroupTankDied();

            Debug.Log($"[Group {GroupId}] ¡Tank caído! El grupo reacciona.");
        }

        Debug.Log($"[Group {GroupId}] -{member.GetEnemy().name} — " +
                  $"{Members.Count} miembros restantes.");
    }


    public Character GetSharedTarget()
    {
        if (HasTank && ActiveTank.GetAIController().CurrentTarget != null)
            return ActiveTank.GetAIController().CurrentTarget;

        return Members
            .Where(m => m.IsAlive && m.GetAIController()?.CurrentTarget != null)
            .Select(m => m.GetAIController().CurrentTarget)
            .FirstOrDefault();
    }

    public bool IsEmpty => Members.Count == 0;
}