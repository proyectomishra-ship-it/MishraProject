using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Celda individual en la grilla de items del inventario.
/// Muestra el icono y la cantidad. Al hacer click notifica al InventoryUI.
/// </summary>
public class InventoryItemUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Referencias")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image selectionHighlight;

    private ItemData itemData;
    private int quantity;

    public System.Action<ItemData, int> OnSelected;

    public void Setup(ItemData item, int qty)
    {
        this.itemData = item;
        this.quantity = qty;

        if (iconImage != null)
        {
            iconImage.sprite = item.Icon;
            iconImage.enabled = item.Icon != null;
        }

        if (quantityText != null)
        {
            quantityText.text = qty > 1 ? $"x{qty}" : "";
            quantityText.enabled = qty > 1;
        }

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectionHighlight != null)
            selectionHighlight.enabled = selected;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnSelected?.Invoke(itemData, quantity);
    }
}