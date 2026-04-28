/// <summary>
/// Resultado inmutable de una tirada de loot.
/// Solo datos, cero lógica.
/// ACCIÓN: archivo nuevo en Assets/Scripts/Drops/
/// </summary>
public readonly struct LootDrop
{
    public readonly ItemData Item;
    public readonly int      Quantity;

    public LootDrop(ItemData item, int quantity)
    {
        Item     = item;
        Quantity = quantity;
    }
}
