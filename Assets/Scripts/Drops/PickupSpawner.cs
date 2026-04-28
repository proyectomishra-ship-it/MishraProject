using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Posiciona y spawnea ItemPickups como NetworkObjects alrededor de un punto.
/// No sabe nada de loot ni de inventario. Solo instancia y spawnea.
/// ACCIÓN: archivo nuevo en Assets/Scripts/Drops/
/// </summary>
public class PickupSpawner
{
    private readonly GameObject prefab;
    private readonly float      radius;
    private readonly float      height;

    public PickupSpawner(GameObject prefab, float radius = 1.5f, float height = 0.5f)
    {
        this.prefab  = prefab;
        this.radius  = radius;
        this.height  = height;
    }

    public void SpawnAll(IReadOnlyList<LootDrop> drops, Vector3 origin)
    {
        for (int i = 0; i < drops.Count; i++)
            SpawnOne(drops[i], origin, i, drops.Count);
    }

    private void SpawnOne(LootDrop drop, Vector3 origin, int index, int total)
    {
        Vector3 pos = origin + Offset(index, total) + Vector3.up * height;
        var     obj = Object.Instantiate(prefab, pos, Quaternion.identity);

        if (!obj.TryGetComponent<ItemPickup>(out var pickup) ||
            !obj.TryGetComponent<NetworkObject>(out var netObj))
        {
            Debug.LogError("[PickupSpawner] Prefab inválido: falta ItemPickup o NetworkObject.");
            Object.Destroy(obj);
            return;
        }

        pickup.Setup(drop.Item, drop.Quantity);
        netObj.Spawn();
    }

    private Vector3 Offset(int index, int total)
    {
        if (total <= 1) return Vector3.zero;
        float angle = (360f / total) * index * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
    }
}
