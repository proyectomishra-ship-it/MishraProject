/// <summary>
/// Resultado de intentar usar un consumible. Permite que la UI muestre un
/// mensaje específico en vez de un simple true/false.
/// </summary>
public enum ConsumeResult
{
    Success,
    NotConsumable,
    NotInInventory,
    NothingToRestore,   // vida/maná ya al máximo: no se gasta la poción
    PlayerDead
}

/// <summary>
/// Lógica de uso de consumibles. Sin red: el servidor la invoca desde
/// Player.UseItemServerRpc. Mismo espíritu que CraftingSystem.
/// </summary>
public static class ConsumableSystem
{
    public static ConsumeResult TryUse(IInventory inventory, ResourceController resources, ItemData item)
    {
        if (item == null || inventory == null || resources == null)
            return ConsumeResult.NotConsumable;

        if (!item.IsUsableConsumable)
            return ConsumeResult.NotConsumable;

        if (!inventory.HasItem(item, 1))
            return ConsumeResult.NotInInventory;

        bool wouldHeal = item.HealthRestore > 0f &&
                         resources.GetCurrentHealth() < resources.GetMaxHealth();
        bool wouldMana = item.ManaRestore > 0f &&
                         resources.GetCurrentMana() < resources.GetMaxMana();

        // No gastar la poción si no tendría ningún efecto.
        if (!wouldHeal && !wouldMana)
            return ConsumeResult.NothingToRestore;

        if (!inventory.RemoveItem(item, 1))
            return ConsumeResult.NotInInventory;

        if (wouldHeal) resources.Heal(item.HealthRestore);
        if (wouldMana) resources.AddMana(item.ManaRestore);

        return ConsumeResult.Success;
    }
}
