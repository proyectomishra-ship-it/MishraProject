using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CombatSlotManager : MonoBehaviour
{
    public static CombatSlotManager Instance;

 
    [SerializeField] private int maxSlots = 8;

   
    [SerializeField] private float flankerRadius = 2.5f;

   
    [SerializeField] private float tankRadius = 1.8f;

   
    private Dictionary<Transform, SlotAssignment> assignments = new();

    private void Awake()
    {
        Instance = this;
    }

    // -------------------------
    // TANK
    // -------------------------


    public Vector3 GetTankSlotPosition(EnemyAIController tank, Transform target)
    {
        var assignment = GetOrCreateAssignment(target);

        if (assignment.Tank == null || assignment.Tank == tank)
            assignment.Tank = tank;

        Vector3 dirToTank = (tank.transform.position - target.position).normalized;
        if (dirToTank == Vector3.zero)
            dirToTank = target.forward;

        return target.position + dirToTank.normalized * tankRadius;
    }

    public void RemoveTank(EnemyAIController tank, Transform target)
    {
        if (target == null) return;
        if (assignments.TryGetValue(target, out var assignment) &&
            assignment.Tank == tank)
            assignment.Tank = null;
    }

    // -------------------------
    // FLANKERS (Goblins)
    // -------------------------

  
    public Vector3 GetFlankerSlotPosition(EnemyAIController flanker, Transform target)
    {
        var assignment = GetOrCreateAssignment(target);

        if (!assignment.Flankers.Contains(flanker))
            assignment.Flankers.Add(flanker);

        int index = assignment.Flankers.IndexOf(flanker);
        int count = Mathf.Min(assignment.Flankers.Count, maxSlots - 1);

        float arcStart = 30f;
        float arcEnd = 330f;
        float arcRange = arcEnd - arcStart;

        float angle = count > 1
            ? arcStart + (index / (float)(count - 1)) * arcRange
            : 180f; 

      
        float targetAngle = Mathf.Atan2(
            target.forward.x, target.forward.z) * Mathf.Rad2Deg;
        float finalAngle = (angle + targetAngle) * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Sin(finalAngle) * flankerRadius,
            0f,
            Mathf.Cos(finalAngle) * flankerRadius
        );

        return target.position + offset;
    }

    public void RemoveFlanker(EnemyAIController flanker, Transform target)
    {
        if (target == null) return;
        if (assignments.TryGetValue(target, out var assignment))
            assignment.Flankers.Remove(flanker);
    }

    // -------------------------
    // RANGED
    // -------------------------

  
    public Vector3 GetRangedSlotPosition(EnemyAIController ranged, Transform target,
        float preferredDistance)
    {
        var assignment = GetOrCreateAssignment(target);

        if (!assignment.Ranged.Contains(ranged))
            assignment.Ranged.Add(ranged);

        int index = assignment.Ranged.IndexOf(ranged);

       
        if (assignment.Tank != null)
        {
            Vector3 behindTank = assignment.Tank.transform.position
                + (assignment.Tank.transform.position - target.position).normalized
                * preferredDistance;

            Vector3 lateral = Vector3.Cross(
                (target.position - assignment.Tank.transform.position).normalized,
                Vector3.up) * (index - assignment.Ranged.Count * 0.5f) * 2f;

            return behindTank + lateral;
        }

        
        float angle = (150f + index * 30f) * Mathf.Deg2Rad;
        return target.position + new Vector3(
            Mathf.Sin(angle) * preferredDistance,
            0f,
            Mathf.Cos(angle) * preferredDistance
        );
    }

    public void RemoveRanged(EnemyAIController ranged, Transform target)
    {
        if (target == null) return;
        if (assignments.TryGetValue(target, out var assignment))
            assignment.Ranged.Remove(ranged);
    }

    // -------------------------
    // HELPERS
    // -------------------------

    private SlotAssignment GetOrCreateAssignment(Transform target)
    {
        if (!assignments.ContainsKey(target))
            assignments[target] = new SlotAssignment();
        return assignments[target];
    }

  
    private void Update()
    {
        var deadTargets = assignments.Keys
            .Where(t => t == null)
            .ToList();

        foreach (var t in deadTargets)
            assignments.Remove(t);
    }

    // -------------------------
    // DEBUG
    // -------------------------

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        foreach (var kvp in assignments)
        {
            if (kvp.Key == null) continue;
            Vector3 targetPos = kvp.Key.position;

            // Tank slot — azul.
            if (kvp.Value.Tank != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(
                    GetTankSlotPosition(kvp.Value.Tank, kvp.Key), 0.3f);
            }

            // Flanker slots — verde.
            Gizmos.color = Color.green;
            foreach (var f in kvp.Value.Flankers)
                Gizmos.DrawWireSphere(
                    GetFlankerSlotPosition(f, kvp.Key), 0.25f);

            // Ranged slots — amarillo.
            Gizmos.color = Color.yellow;
            foreach (var r in kvp.Value.Ranged)
                Gizmos.DrawWireSphere(
                    GetRangedSlotPosition(r, kvp.Key, 10f), 0.25f);
        }
    }

    // -------------------------
    // INNER CLASS
    // -------------------------

    private class SlotAssignment
    {
        public EnemyAIController Tank;
        public List<EnemyAIController> Flankers = new();
        public List<EnemyAIController> Ranged = new();
    }
}