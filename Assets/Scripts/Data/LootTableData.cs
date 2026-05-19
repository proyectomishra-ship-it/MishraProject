using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Define qué puede dropear un enemigo al morir.
/// Solo datos. La lógica de tirada está en WeightedLootRoller.
/// ACCIÓN: archivo nuevo en Assets/Scripts/Data/
/// Crear en: Assets > Create > RPG > Loot Table
/// </summary>
[CreateAssetMenu(menuName = "RPG/Loot Table")]
public class LootTableData : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public ItemData item;
        [Min(0f)] public float weight;
        [Min(1)]  public int   minQty;
        [Min(1)]  public int   maxQty;
    }

    [SerializeField] private List<Entry> entries = new();
    [SerializeField][Range(0f, 1f)] private float dropChance = 0.75f;
    [SerializeField][Min(0)] private int minDrops = 0;
    [SerializeField][Min(0)] private int maxDrops = 1;

    public IReadOnlyList<Entry> Entries    => entries;
    public float                DropChance => dropChance;
    public int                  MinDrops   => minDrops;
    public int                  MaxDrops   => maxDrops;
}
