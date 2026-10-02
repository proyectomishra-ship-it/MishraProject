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
    [Header("Slot")]
    [SerializeField] private EquipmentSlot slotType;

    [Header("Referencias")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI slotLabel;
    [SerializeField] private GameObject emptyOverlay;

    [Header("Colores")]
    [SerializeField] private Color occupiedColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color emptyColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);

    [Header("Fondo de slot vacio (opcional)")]
    [Tooltip("Tinte aplicado a la silueta del slot vacio. El alfa la hace tenue.")]
    [SerializeField] private Color placeholderTint = new Color(1f, 1f, 1f, 0.35f);

    private EquipmentSlot slot;
    private EquipmentController equipment;

    /// <summary>Tipo de equipamiento que representa este slot.</summary>
    public EquipmentSlot Slot => slot;

    public System.Action<EquipmentSlot> OnUnequipRequested;

    /// <summary>
    /// Usado cuando el slot está precolocado en la escena.
    /// Lee el SlotType del Inspector en vez de recibirlo por parámetro.
    /// </summary>
    public void SetupFromScene(EquipmentController equipment)
    {
        Setup(slotType, equipment);
    }

    public void Setup(EquipmentSlot slot, EquipmentController equipment)
    {
        this.slot = slot;
        this.equipment = equipment;

        if (slotLabel != null)
            slotLabel.text = GetSlotLabel(slot);

        Refresh();
    }

    /// <summary>
    /// Asigna la silueta que se muestra mientras el slot esta vacio (como en
    /// la mayoria de los juegos: un casco tenue en el slot del casco, etc.).
    /// Usa la imagen de EmptyOverlay. Si hay silueta, oculta la etiqueta de
    /// texto; sin silueta, EmptyOverlay queda como un velo oscuro y la
    /// etiqueta se sigue mostrando.
    /// </summary>
    public void SetPlaceholder(Sprite sprite)
    {
        if (emptyOverlay != null && emptyOverlay.TryGetComponent(out Image overlayImage))
        {
            overlayImage.sprite = sprite;
            overlayImage.preserveAspect = true;
            overlayImage.color = sprite != null ? placeholderTint : emptyColor;
        }

        if (slotLabel != null)
            slotLabel.gameObject.SetActive(sprite == null);
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
        // Logs de diagnostico: dicen en que punto se corta un clic que
        // "no responde". Se pueden quitar cuando la UI de equipamiento
        // este terminada.
        if (equipment == null)
        {
            Debug.LogWarning($"[SlotUI] Clic en '{name}' ({slot}): equipment es NULL, no se puede desequipar.");
            return;
        }

        if (!equipment.IsOccupied(slot))
        {
            Debug.Log($"[SlotUI] Clic en '{name}' ({slot}): el slot esta vacio, no hay nada que desequipar.");
            return;
        }

        Debug.Log($"[SlotUI] Clic en '{name}' ({slot}): solicitando desequipar.");
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