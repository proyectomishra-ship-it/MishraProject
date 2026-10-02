using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Conecta la muerte del enemigo con el sistema de drops.
/// 
/// El DropController no conoce los objetos individuales.
/// La LootTable determina qué prefabs pueden caer.
/// </summary>
public class DropController : NetworkBehaviour
{
    [Header("Loot")]
    [SerializeField]
    private LootTableData lootTable;

    [Header("Spawn")]
    [SerializeField]
    private float spreadRadius = 1.5f;

    private WeightedLootRoller roller;
    private PickupSpawner spawner;

    private void Awake()
    {
        roller =
            new WeightedLootRoller(lootTable);

        spawner =
            new PickupSpawner(spreadRadius);
    }

    public void OnEnemyDied()
    {
        if (!IsServer)
            return;

        if (lootTable == null)
        {
            Debug.LogError(
                $"[DropController] '{name}' no tiene " +
                "LootTableData asignada.");

            return;
        }

        var drops =
            roller.Roll();

        Debug.Log(
            $"[DropController] '{name}' generó " +
            $"{drops.Count} drop(s).");

        if (drops.Count == 0)
            return;

        spawner.SpawnAll(
            drops,
            transform.position);
    }
}