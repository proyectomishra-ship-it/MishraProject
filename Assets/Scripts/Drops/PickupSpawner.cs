using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Instancia y spawnea los prefabs físicos
/// seleccionados por el sistema de loot.
///
/// No conoce ItemData.
/// No conoce qué objeto representa el pickup.
/// Solo instancia el prefab recibido.
/// </summary>
public class PickupSpawner
{
    private readonly float radius;
    private readonly float height;

    public PickupSpawner(
        float radius = 1.5f,
        float height = 0.5f)
    {
        this.radius = radius;
        this.height = height;
    }

    /// <summary>
    /// Spawnea todos los drops recibidos alrededor
    /// de una posición de origen.
    /// </summary>
    public void SpawnAll(
        IReadOnlyList<LootDrop> drops,
        Vector3 origin)
    {
        if (drops == null || drops.Count == 0)
            return;

        for (int i = 0; i < drops.Count; i++)
        {
            SpawnOne(
                drops[i],
                origin,
                i,
                drops.Count);
        }
    }

    /// <summary>
    /// Instancia y spawnea un único pickup.
    /// </summary>
    private void SpawnOne(
        LootDrop drop,
        Vector3 origin,
        int index,
        int total)
    {
        if (drop.PickupPrefab == null)
            return;

        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        Vector3 position =
            origin +
            Offset(index, total) +
            Vector3.up * height;

        GameObject obj = Object.Instantiate(
            drop.PickupPrefab,
            position,
            Quaternion.identity);

        if (!obj.TryGetComponent<NetworkObject>(
                out NetworkObject networkObject))
        {
            Object.Destroy(obj);
            return;
        }

        if (!obj.TryGetComponent<ItemPickup>(
                out ItemPickup itemPickup))
        {
            Object.Destroy(obj);
            return;
        }

        itemPickup.SetQuantity(drop.Quantity);

        networkObject.Spawn();
    }

    /// <summary>
    /// Calcula el desplazamiento horizontal
    /// del pickup alrededor del origen.
    /// </summary>
    private Vector3 Offset(
        int index,
        int total)
    {
        if (total <= 1)
            return Vector3.zero;

        float angle =
            (360f / total) *
            index *
            Mathf.Deg2Rad;

        return new Vector3(
            Mathf.Sin(angle),
            0f,
            Mathf.Cos(angle)) * radius;
    }
}