using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Celda individual en la grilla de recetas (pestaña "Crafteo" del inventario).
/// Mismo patrón que InventoryItemUI, con el agregado de un color de fondo
/// que indica si la receta se puede craftear con el inventario/oro actual.
/// ACCIÓN: archivo nuevo en Assets/Scripts/UI/
/// </summary>
public class CraftingRecipeUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Referencias")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image selectionHighlight;
    [SerializeField] private Image backgroundImage;

    [Header("Colores de fondo según disponibilidad")]
    [SerializeField] private Color canCraftColor = new Color(0.22f, 0.22f, 0.22f, 0.85f);
    [SerializeField] private Color cannotCraftColor = new Color(0.18f, 0.08f, 0.08f, 0.85f);

    public CraftingRecipeData Recipe { get; private set; }
    private int recipeId;

    public System.Action<CraftingRecipeData, int> OnSelected;

    public void Setup(CraftingRecipeData recipe, int recipeId, bool canCraft)
    {
        Recipe = recipe;
        this.recipeId = recipeId;

        var output = recipe != null ? recipe.Output : null;

        if (iconImage != null)
        {
            iconImage.sprite = output != null ? output.Icon : null;
            iconImage.enabled = output != null && output.Icon != null;
        }

        if (quantityText != null)
        {
            bool showQty = recipe != null && recipe.OutputQuantity > 1;
            quantityText.text = showQty ? $"x{recipe.OutputQuantity}" : "";
            quantityText.enabled = showQty;
        }

        SetCraftable(canCraft);
        SetSelected(false);
    }

    public void SetCraftable(bool canCraft)
    {
        if (backgroundImage != null)
            backgroundImage.color = canCraft ? canCraftColor : cannotCraftColor;
    }

    public void SetSelected(bool selected)
    {
        if (selectionHighlight != null)
            selectionHighlight.enabled = selected;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnSelected?.Invoke(Recipe, recipeId);
    }
}
