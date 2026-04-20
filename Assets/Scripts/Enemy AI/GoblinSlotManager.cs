using System.Collections.Generic;
using UnityEngine;

public class GoblinSlotManager : MonoBehaviour
{
    public static GoblinSlotManager Instance;

    private Dictionary<Transform, List<GoblinAIController>> assignments = new();

    private int maxSlots = 8;
    private float radius = 2.5f;

    private void Awake()
    {
        Instance = this;
    }

    public Vector3 GetSlotPosition(GoblinAIController goblin, Transform target)
    {
        if (!assignments.ContainsKey(target))
            assignments[target] = new List<GoblinAIController>();

        var list = assignments[target];

        if (!list.Contains(goblin))
            list.Add(goblin);

        int index = list.IndexOf(goblin);
        int count = Mathf.Min(list.Count, maxSlots);

        float angle = (index / (float)count) * 360f;

        float rad = angle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Sin(rad) * radius,
            0,
            Mathf.Cos(rad) * radius
        );

        return target.position + offset;
    }

    public void RemoveGoblin(GoblinAIController goblin, Transform target)
    {
        if (target == null) return;

        if (assignments.TryGetValue(target, out var list))
        {
            list.Remove(goblin);
        }
    }
}