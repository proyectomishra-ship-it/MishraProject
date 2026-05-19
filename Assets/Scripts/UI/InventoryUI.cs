using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [Header("Panel principal")]
    [SerializeField] private GameObject inventoryPanel;

    [Header("Camara de preview del personaje")]
    [SerializeField] private Camera inventoryCamera;

    [Header("Panel izquierdo — Personaje y equipamiento")]
    [SerializeField] private RawImage characterPreview;
    [SerializeField] private Transform equipmentContainer;
    [SerializeField] private InventorySlotUI slotPrefab;

    [Header("Panel central — Grilla de items")]
    [SerializeField] private Transform itemGridContainer;
    [SerializeField] private InventoryItemUI itemCellPrefab;

    [Header("Panel derecho — Detalle del item")]
    [SerializeField] private Image detailIcon;
    [SerializeField] private TextMeshProUGUI detailName;
    [SerializeField] private TextMeshProUGUI detailDescription;
    [SerializeField] private TextMeshProUGUI detailQuantity;
    [SerializeField] private Button equipButton;
    [SerializeField] private TextMeshProUGUI equipButtonText;
    [SerializeField] private GameObject detailPanel;

    private InventoryController inventory;
    private EquipmentController equipment;
    private Player player;

    private readonly List<InventorySlotUI> slotUIs = new();
    private readonly List<InventoryItemUI> itemUIs = new();

    private ItemData selectedItem;
    private bool isOpen = false;

    // =========================
    // INIT
    // =========================

    public void Initialize(Player player, InventoryController inventory, EquipmentController equipment)
    {
        this.player = player;
        this.inventory = inventory;
        this.equipment = equipment;

        BuildEquipmentSlots();

        inventory.OnChanged += RefreshItemGrid;
        equipment.OnSlotChanged += (_, __) => RefreshEquipmentSlots();

        if (equipButton != null) equipButton.onClick.AddListener(OnEquipButtonClicked);
        if (detailPanel != null) detailPanel.SetActive(false);

        inventoryPanel?.SetActive(false);
        inventoryCamera?.gameObject.SetActive(false);

        Debug.Log("[InventoryUI] Inicializado.");
    }

    private void OnDestroy()
    {
        if (inventory != null) inventory.OnChanged -= RefreshItemGrid;
    }

    // =========================
    // INPUT
    // =========================

    private void Update()
    {
        if (player == null) return;
        if (Keyboard.current.tabKey.wasPressedThisFrame)
            ToggleInventory();
    }

    private void ToggleInventory()
    {
        isOpen = !isOpen;
        inventoryPanel?.SetActive(isOpen);
        inventoryCamera?.gameObject.SetActive(isOpen);

        Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isOpen;

        if (isOpen)
        {
            RefreshEquipmentSlots();
            RefreshItemGrid();
            ClearDetail();
        }

        Debug.Log($"[InventoryUI] {(isOpen ? "Abierto" : "Cerrado")}");
    }

    // =========================
    // EQUIPAMIENTO
    // =========================

    private void BuildEquipmentSlots()
    {
        if (equipmentContainer == null || slotPrefab == null) return;

        foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
        {
            var slotUI = Instantiate(slotPrefab, equipmentContainer);
            slotUI.Setup(slot, equipment);
            slotUI.OnUnequipRequested += RequestUnequip;
            slotUIs.Add(slotUI);
        }
    }

    private void RefreshEquipmentSlots()
    {
        foreach (var s in slotUIs) s.Refresh();
    }

    private void RequestUnequip(EquipmentSlot slot)
    {
        player?.RequestUnequip(slot);
    }

    // =========================
    // GRILLA DE ITEMS
    // =========================

    private void RefreshItemGrid()
    {
        foreach (var ui in itemUIs) Destroy(ui.gameObject);
        itemUIs.Clear();
        selectedItem = null;

        foreach (var (item, qty) in inventory.GetAll())
        {
            var cell = Instantiate(itemCellPrefab, itemGridContainer);
            cell.Setup(item, qty);
            cell.OnSelected += ShowDetail;
            itemUIs.Add(cell);
        }
    }

    // =========================
    // PANEL DETALLE
    // =========================

    private void ShowDetail(ItemData item, int qty)
    {
        selectedItem = item;

        if (detailPanel != null) detailPanel.SetActive(true);
        if (detailIcon != null) { detailIcon.sprite = item.Icon; detailIcon.enabled = item.Icon != null; }
        if (detailName != null) detailName.text = item.ItemName;
        if (detailDescription != null) detailDescription.text = item.GenerateStatsDescription();
        if (detailQuantity != null) detailQuantity.text = qty > 1 ? $"Cantidad: {qty}" : "";

        bool isEquippable = item is IEquippable;
        if (equipButton != null)
        {
            equipButton.gameObject.SetActive(isEquippable);
            if (isEquippable && equipButtonText != null)
            {
                var equippable = item as IEquippable;
                equipButtonText.text = equipment.IsOccupied(equippable.Slot) ? "Reemplazar" : "Equipar";
            }
        }
    }

    private void ClearDetail()
    {
        selectedItem = null;
        if (detailPanel != null) detailPanel.SetActive(false);
    }

    private void OnEquipButtonClicked()
    {
        if (selectedItem == null) return;
        player?.RequestEquip(selectedItem);
    }
}

// Extension para generar descripcion automatica de stats
public static class ItemDataExtensions
{
    public static string GenerateStatsDescription(this ItemData item)
    {
        if (item is not IEquippable equippable || equippable.Modifiers == null || equippable.Modifiers.Count == 0)
            return string.IsNullOrEmpty(item.Description) ? "Sin stats." : item.Description;

        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrEmpty(item.Description))
            sb.AppendLine(item.Description).AppendLine();

        foreach (var mod in equippable.Modifiers)
        {
            string statName = GetStatName(mod.stat);
            string sign = mod.value >= 0 ? "+" : "";
            sb.AppendLine($"{statName}: {sign}{mod.value}");
        }

        // Info extra para armas
        if (item is WeaponData weapon)
        {
            sb.AppendLine($"Velocidad de ataque: {weapon.AttackSpeed}");
            sb.AppendLine($"Multiplicador pesado: x{weapon.HeavyMultiplier}");
            if (weapon.IsRanged)
                sb.AppendLine("Tipo: Ranged");
        }

        return sb.ToString().TrimEnd();
    }

    private static string GetStatName(StatType stat) => stat switch
    {
        StatType.Attack => "Ataque",
        StatType.AttackRange => "Rango",
        StatType.Defense => "Defensa",
        StatType.MaxHealth => "Vida maxima",
        StatType.MaxMana => "Mana maximo",
        StatType.Speed => "Velocidad",
        StatType.Agility => "Agilidad",
        StatType.CriticalChance => "Critico",
        StatType.Dexterity => "Destreza",
        StatType.Intelligence => "Inteligencia",
        StatType.Vitality => "Vitalidad",
        StatType.Resistance => "Resistencia",
        StatType.Luck => "Suerte",
        _ => stat.ToString()
    };
}