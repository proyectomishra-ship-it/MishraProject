using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Receta de crafteo: una lista de ingredientes (item + cantidad) que se
/// consumen del inventario para producir un item resultado.
///
/// Mismo patrón que LootTableData: una lista serializable de entradas,
/// pura data, sin lógica. La lógica de crafteo en sí vive en CraftingSystem.
///
/// ACCIÓN: archivo nuevo en Assets/Scripts/Data/
/// Crear instancias en: Assets > Create > RPG > Crafting Recipe
/// </summary>
[CreateAssetMenu(menuName = "RPG/Crafting Recipe")]
public class CraftingRecipeData : ScriptableObject
{
    [Serializable]
    public struct Ingredient
    {
        public ItemData item;
        [Min(1)] public int quantity;
    }

    [Header("Resultado")]
    [SerializeField] private ItemData outputItem;
    [SerializeField, Min(1)] private int outputQuantity = 1;

    [Header("Ingredientes requeridos")]
    [SerializeField] private List<Ingredient> ingredients = new();

    [Header("Costo adicional (opcional)")]
    [Tooltip("Oro requerido además de los ingredientes. Dejar en 0 si la receta no cuesta oro.")]
    [SerializeField, Min(0)] private int goldCost = 0;

    public ItemData Output => outputItem;
    public int OutputQuantity => outputQuantity;
    public IReadOnlyList<Ingredient> Ingredients => ingredients;
    public int GoldCost => goldCost;
}
