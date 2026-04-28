using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Conecta la muerte del enemigo con el sistema de drops.
/// Agregar este componente al prefab de cada enemigo junto con Enemy.cs.
/// ACCIÓN: archivo nuevo en Assets/Scripts/Drops/
///
/// REQUERIMIENTO — agregar en Enemy.Die() antes de base.Die():
///     GetComponent&lt;DropController&gt;()?.OnEnemyDied();
/// </summary>
public class DropController : NetworkBehaviour
{
    [SerializeField] private LootTableData lootTable;
    [SerializeField] private GameObject    pickupPrefab;
    [SerializeField] private float         spreadRadius = 1.5f;

    private WeightedLootRoller roller;
    private PickupSpawner      spawner;

    private void Awake()
    {
        roller  = new WeightedLootRoller(lootTable);
        spawner = new PickupSpawner(pickupPrefab, spreadRadius);
    }

    /// <summary>
    /// Llamar desde Enemy.Die() en el servidor.
    /// Tira la tabla de loot y spawnea los items.
    /// </summary>
    public void OnEnemyDied()
    {
        if (!IsServer) return;

        var drops = roller.Roll();
        if (drops.Count > 0)
            spawner.SpawnAll(drops, transform.position);
    }
}
