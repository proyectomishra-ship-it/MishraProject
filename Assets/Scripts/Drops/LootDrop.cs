using UnityEngine;

/// <summary>
/// Resultado de una tirada de loot.
/// Contiene el prefab físico que debe aparecer
/// y la cantidad de unidades que representa.
/// 
/// Solo datos, cero lógica.
/// </summary>
public readonly struct LootDrop
{
    public readonly GameObject PickupPrefab;
    public readonly int Quantity;

    public LootDrop(GameObject pickupPrefab, int quantity)
    {
        PickupPrefab = pickupPrefab;
        Quantity = quantity;
    }
}