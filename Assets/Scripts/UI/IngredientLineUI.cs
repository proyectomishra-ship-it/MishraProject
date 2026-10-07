using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Una línea de la lista de ingredientes: icono + "Nombre  tengo / necesito".
/// Si InventoryUI no tiene asignado este prefab, usa la línea de solo texto
/// de siempre, así que es opcional.
/// </summary>
public class IngredientLineUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI label;

    public void Setup(ItemData item, int have, int need, Color color)
    {
        if (iconImage != null)
        {
            iconImage.sprite = item != null ? item.Icon : null;
            iconImage.enabled = item != null && item.Icon != null;
        }

        if (label != null)
        {
            label.text = $"{(item != null ? item.ItemName : "???")}  {have} / {need}";
            label.color = color;
        }
    }
}
