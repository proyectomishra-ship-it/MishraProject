using System.Collections.Generic;
using UnityEngine;

public class WeightedLootRoller
{
    private readonly LootTableData table;

    public WeightedLootRoller(LootTableData table)
    {
        this.table = table;
    }

    public List<LootDrop> Roll()
    {
        var result = new List<LootDrop>();

        if (table == null)
            return result;

        if (Random.value > table.DropChance)
            return result;

        int count =
            Random.Range(
                table.MinDrops,
                table.MaxDrops + 1);

        if (count <= 0)
            return result;

        var pool =
            new List<LootTableData.Entry>(
                table.Entries);

        float total =
            TotalWeight(pool);

        if (total <= 0f)
            return result;

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            var entry =
                Select(pool, total);

            if (entry.pickupPrefab == null)
            {
                Debug.LogWarning(
                    "[LootRoller] La entrada seleccionada " +
                    "no tiene Pickup Prefab asignado.");

                pool.Remove(entry);
                total -= entry.weight;
                continue;
            }

            int quantity =
                Random.Range(
                    entry.minQty,
                    entry.maxQty + 1);

            result.Add(
                new LootDrop(
                    entry.pickupPrefab,
                    quantity));

            pool.Remove(entry);
            total -= entry.weight;
        }

        return result;
    }

    private LootTableData.Entry Select(
        List<LootTableData.Entry> pool,
        float total)
    {
        float roll =
            Random.value * total;

        float cumulative = 0f;

        foreach (var entry in pool)
        {
            cumulative += entry.weight;

            if (roll <= cumulative)
                return entry;
        }

        return pool[pool.Count - 1];
    }

    private static float TotalWeight(
        List<LootTableData.Entry> pool)
    {
        float total = 0f;

        foreach (var entry in pool)
        {
            total += entry.weight;
        }

        return total;
    }
}