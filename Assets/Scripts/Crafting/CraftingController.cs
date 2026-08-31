using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Orquestador de crafteo del lado del servidor. Usa CraftingSystem (lógica
/// pura) contra el inventario y el oro del dueño. Si el crafteo tiene éxito,
/// el resultado se ve solo: InventoryNetworkSync y la NetworkVariable de
/// GoldController ya se encargan de replicarlo al cliente. Esta clase solo
/// decide SI se puede craftear y ejecuta el intercambio.
///
/// ACCIÓN: archivo nuevo en Assets/Scripts/Crafting/
/// Agregar este componente al prefab del Player, junto a GoldController.
/// </summary>
public class CraftingController : NetworkBehaviour
{
    private InventoryController inventory;
    private GoldController gold;

    public void Initialize(Character owner)
    {
        inventory = owner.GetInventory();
        gold = owner.GetComponent<GoldController>();
    }

    /// <summary>
    /// Intenta craftear la receta indicada. Solo tiene efecto en el servidor.
    /// </summary>
    public CraftResult TryCraft(CraftingRecipeData recipe)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"[CraftingController] TryCraft llamado desde cliente en {name}.");
            return CraftResult.InvalidRecipe;
        }

        if (inventory == null || recipe == null)
            return CraftResult.InvalidRecipe;

        int currentGold = gold != null ? gold.Gold : 0;
        var result = CraftingSystem.TryCraft(inventory, recipe, currentGold);

        if (result == CraftResult.Success && recipe.GoldCost > 0)
            gold?.RemoveGold(recipe.GoldCost);

        Debug.Log($"[Crafting] {name} intentó craftear '{recipe.name}': {result}");

        return result;
    }
}
