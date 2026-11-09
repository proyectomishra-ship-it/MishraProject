/// <summary>
/// Rareza de items. El color de cada rareza vive en UITheme.GetRarityColor().
/// Todavia no esta enlazada a ItemInstance: se usara en la fase del inventario.
/// </summary>
public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}
