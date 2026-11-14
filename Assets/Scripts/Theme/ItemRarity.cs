/// <summary>
/// Rareza de items. El orden es el de progresion: no reordenar (se serializa
/// como indice). El color de cada rareza vive en UITheme.GetRarityColor().
/// </summary>
public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public static class ItemRarityExtensions
{
    public static string DisplayName(this ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Uncommon: return "Poco común";
            case ItemRarity.Rare: return "Raro";
            case ItemRarity.Epic: return "Épico";
            case ItemRarity.Legendary: return "Legendario";
            default: return "Común";
        }
    }
}
