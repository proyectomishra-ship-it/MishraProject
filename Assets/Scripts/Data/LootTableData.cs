using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Loot Table")]
public class LootTableData : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        [Tooltip("Prefab físico completo del objeto que puede caer.")]
        public GameObject pickupPrefab;

        [Min(0f)]
        public float weight;

        [Min(1)]
        public int minQty;

        [Min(1)]
        public int maxQty;
    }

    [Header("Loot")]
    [SerializeField]
    private List<Entry> entries = new();

    [Header("Drop Settings")]
    [SerializeField]
    [Range(0f, 1f)]
    private float dropChance = 0.75f;

    [SerializeField]
    [Min(0)]
    private int minDrops = 0;

    [SerializeField]
    [Min(0)]
    private int maxDrops = 1;

    public IReadOnlyList<Entry> Entries => entries;
    public float DropChance => dropChance;
    public int MinDrops => minDrops;
    public int MaxDrops => maxDrops;
}