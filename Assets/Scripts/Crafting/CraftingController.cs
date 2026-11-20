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
    private Character owner;

    public void Initialize(Character owner)
    {
        this.owner = owner;
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

        if (result == CraftResult.Success)
            RefreshEquipmentAfterCraft(recipe);

        // Misiones secundarias: cuenta como item crafteado.
        if (result == CraftResult.Success && NetworkQuestManager.Instance != null)
            NetworkQuestManager.Instance.ReportItemCrafted(recipe.Output, recipe.OutputQuantity, owner as Player);

        Debug.Log($"[Crafting] {name} intentó craftear '{recipe.name}': {result}");

        return result;
    }

    /// <summary>
    /// Los items equipados siguen contandose en el inventario, asi que una mejora
    /// puede consumir el arma que llevas puesta. Si ya no queda ninguna copia en la
    /// mochila, se desequipa (para no quedar con un item fantasma) y, si el
    /// resultado es equipable en ese mismo slot y la clase lo permite, se equipa
    /// directamente: mejorar el arma en uso la deja puesta.
    /// </summary>
    private void RefreshEquipmentAfterCraft(CraftingRecipeData recipe)
    {
        var equipment = owner != null ? owner.GetEquipment() : null;
        if (equipment == null) return;

        var player = owner as Player;
        var classData = player != null ? player.ClassData : null;

        foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
        {
            if (equipment.GetEquipped(slot) is not ItemData equippedItem) continue;
            if (inventory.GetQuantity(equippedItem) > 0) continue;

            equipment.Unequip(slot);

            if (recipe.Output is IEquippable upgraded &&
                upgraded.Slot == slot &&
                upgraded.CanBeUsedBy(classData) &&
                inventory.GetQuantity(recipe.Output) > 0)
            {
                equipment.Equip(upgraded);
            }
        }
    }
}
