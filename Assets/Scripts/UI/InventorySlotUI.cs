using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Slot individual de equipamiento en la UI.
/// Muestra el item equipado y permite desequiparlo al hacer click.
/// </summary>
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Referencias")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI slotLabel;
    [SerializeField] private GameObject emptyOverlay;

    [Header("Colores")]
    [SerializeField] private Color occupiedColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color emptyColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);

    private EquipmentSlot slot;
    private EquipmentController equipment;

    public System.Action<EquipmentSlot> OnUnequipRequested;

    public void Setup(EquipmentSlot slot, EquipmentController equipment)
    {
        this.slot = slot;
        this.equipment = equipment;

        if (slotLabel != null)
            slotLabel.text = GetSlotLabel(slot);

        Refresh();
    }

    public void Refresh()
    {
        var item = equipment?.GetEquipped(slot);
        bool hasItem = item != null;

        if (backgroundImage != null)
            backgroundImage.color = hasItem ? occupiedColor : emptyColor;

        if (emptyOverlay != null)
            emptyOverlay.SetActive(!hasItem);

        if (iconImage != null)
        {
            iconImage.gameObject.SetActive(hasItem);
            if (hasItem && item is ItemData itemData && itemData.Icon != null)
                iconImage.sprite = itemData.Icon;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (equipment == null || !equipment.IsOccupied(slot)) return;
        OnUnequipRequested?.Invoke(slot);
    }

    private string GetSlotLabel(EquipmentSlot s) => s switch
    {
        EquipmentSlot.Weapon => "Arma",
        EquipmentSlot.Helmet => "Casco",
        EquipmentSlot.Chest => "Pecho",
        EquipmentSlot.Legs => "Piernas",
        EquipmentSlot.Boots => "Botas",
        EquipmentSlot.Ring => "Anillo",
        EquipmentSlot.Amulet => "Amuleto",
        _ => s.ToString()
    };
}