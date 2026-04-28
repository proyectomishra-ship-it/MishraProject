using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Calcula los drops de una muerte usando pesos relativos.
/// C# puro: sin Unity (excepto Random), sin red.
/// ACCIÓN: archivo nuevo en Assets/Scripts/Drops/
/// </summary>
public class WeightedLootRoller
{
    private readonly LootTableData table;

    public WeightedLootRoller(LootTableData table) => this.table = table;

    public List<LootDrop> Roll()
    {
        var result = new List<LootDrop>();
        if (table == null || Random.value > table.DropChance) return result;

        int count = Random.Range(table.MinDrops, table.MaxDrops + 1);
        if (count <= 0) return result;

        var   pool  = new List<LootTableData.Entry>(table.Entries);
        float total = TotalWeight(pool);

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            var entry = Select(pool, total);
            if (entry.item == null) continue;

            int qty = Random.Range(entry.minQty, entry.maxQty + 1);
            result.Add(new LootDrop(entry.item, qty));

            // Evitar que el mismo item caiga dos veces en la misma tirada.
            pool.Remove(entry);
            total -= entry.weight;
        }

        return result;
    }

    private LootTableData.Entry Select(List<LootTableData.Entry> pool, float total)
    {
        float roll  = Random.value * total;
        float cumul = 0f;

        foreach (var e in pool)
        {
            cumul += e.weight;
            if (roll <= cumul) return e;
        }

        return pool[pool.Count - 1];
    }

    private static float TotalWeight(List<LootTableData.Entry> pool)
    {
        float t = 0f;
        foreach (var e in pool) t += e.weight;
        return t;
    }
}
