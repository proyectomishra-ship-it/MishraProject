/// <summary>
/// Resultado de un intento de crafteo. Permite que la UI muestre feedback
/// específico ("faltan materiales", "falta oro"...) en vez de un simple
/// true/false.
/// </summary>
public enum CraftResult
{
    Success,
    MissingIngredients,
    MissingGold,
    InventoryFull,
    InvalidRecipe
}

/// <summary>
/// Lógica pura de crafteo. Sin Unity, sin red. 100% testeable — mismo
/// espíritu que InventoryStore. El servidor la usa como fuente de verdad
/// a través de CraftingController.
/// ACCIÓN: archivo nuevo en Assets/Scripts/Crafting/
/// </summary>
public static class CraftingSystem
{
    public static bool CanCraft(IInventory inventory, CraftingRecipeData recipe, int currentGold)
        => CheckRequirements(inventory, recipe, currentGold) == CraftResult.Success;

    public static CraftResult CheckRequirements(IInventory inventory, CraftingRecipeData recipe, int currentGold)
    {
        if (recipe == null || recipe.Output == null || inventory == null)
            return CraftResult.InvalidRecipe;

        if (recipe.GoldCost > 0 && currentGold < recipe.GoldCost)
            return CraftResult.MissingGold;

        foreach (var ing in recipe.Ingredients)
        {
            if (ing.item == null) continue;
            if (!inventory.HasItem(ing.item, ing.quantity))
                return CraftResult.MissingIngredients;
        }

        return CraftResult.Success;
    }

    /// <summary>
    /// Intenta craftear: remueve los ingredientes y agrega el resultado.
    /// No toca el oro — de eso se encarga CraftingController, que tiene
    /// acceso a GoldController. Esta clase solo conoce IInventory.
    /// </summary>
    public static CraftResult TryCraft(IInventory inventory, CraftingRecipeData recipe, int currentGold)
    {
        var check = CheckRequirements(inventory, recipe, currentGold);
        if (check != CraftResult.Success) return check;

        foreach (var ing in recipe.Ingredients)
        {
            if (ing.item == null) continue;
            inventory.RemoveItem(ing.item, ing.quantity);
        }

        if (!inventory.AddItem(recipe.Output, recipe.OutputQuantity))
        {
            // No debería pasar salvo que el inventario esté al límite justo
            // en este momento. Devolvemos los ingredientes para no perderlos.
            foreach (var ing in recipe.Ingredients)
            {
                if (ing.item == null) continue;
                inventory.AddItem(ing.item, ing.quantity);
            }
            return CraftResult.InventoryFull;
        }

        return CraftResult.Success;
    }
}
