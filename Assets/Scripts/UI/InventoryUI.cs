using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Netcode;
using TMPro;

/// <summary>
/// UI principal del inventario. Se abre/cierra con Tab.
///
/// LAYOUT:
///   Panel izquierdo  → modelo 3D del personaje + slots de equipamiento
///   Panel central    → grilla de iconos de items
///   Panel derecho    → detalle del item seleccionado + boton equipar
///
/// SETUP EN UNITY:
///   Ver comentarios de cada campo en el Inspector.
///   La RenderTexture para el modelo 3D se configura por separado (ver abajo).
/// </summary>
public class InventoryUI : NetworkBehaviour
{
    [Header("Panel principal")]
    [SerializeField] private GameObject inventoryPanel;

    [Header("Panel izquierdo — Personaje y equipamiento")]
    [SerializeField] private RawImage characterPreview;   // Muestra la RenderTexture
    [SerializeField] private Transform equipmentContainer; // Padre de los slots
    [SerializeField] private InventorySlotUI slotPrefab;

    [Header("Panel central — Grilla de items")]
    [SerializeField] private Transform itemGridContainer;  // GridLayoutGroup
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

    private readonly List<InventorySlotUI> slotUIs = new();
    private readonly List<InventoryItemUI> itemUIs = new();

    private ItemData selectedItem;
    private int selectedQty;
    private bool isOpen = false;

    // =========================
    // INIT
    // =========================

    public void Initialize(InventoryController inventory, EquipmentController equipment)
    {
        this.inventory = inventory;
        this.equipment = equipment;

        BuildEquipmentSlots();

        inventory.OnChanged += RefreshItemGrid;
        equipment.OnSlotChanged += (_, __) => RefreshEquipmentSlots();

        if (equipButton != null) equipButton.onClick.AddListener(OnEquipButtonClicked);
        if (detailPanel != null) detailPanel.SetActive(false);

        inventoryPanel?.SetActive(false);

        Debug.Log("[InventoryUI] Inicializado.");
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (inventory != null) inventory.OnChanged -= RefreshItemGrid;
    }

    // =========================
    // INPUT
    // =========================

    private void Update()
    {
        if (!IsOwner) return;
        if (Keyboard.current.tabKey.wasPressedThisFrame)
            ToggleInventory();
    }

    private void ToggleInventory()
    {
        isOpen = !isOpen;
        inventoryPanel?.SetActive(isOpen);

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
        if (!IsOwner) return;
        UnequipServerRpc((int)slot);
    }

    [ServerRpc]
    private void UnequipServerRpc(int slotIndex)
    {
        bool ok = equipment.Unequip((EquipmentSlot)slotIndex);
        Debug.Log($"[InventoryUI] Desequipado slot {(EquipmentSlot)slotIndex}: {ok}");
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
        selectedQty = qty;

        // Highlight celda seleccionada
        foreach (var ui in itemUIs)
            ui.SetSelected(false);

        // Buscar la celda correspondiente y marcarla
        foreach (var ui in itemUIs)
        {
            // Usamos reflection indirecta via OnSelected — simplemente refrescamos todo
        }

        if (detailPanel != null) detailPanel.SetActive(true);
        if (detailIcon != null)
        {
            detailIcon.sprite = item.Icon;
            detailIcon.enabled = item.Icon != null;
        }
        if (detailName != null) detailName.text = item.ItemName;
        if (detailDescription != null) detailDescription.text = item.Description;
        if (detailQuantity != null) detailQuantity.text = qty > 1 ? $"Cantidad: {qty}" : "";

        // Configurar boton equipar
        bool isEquippable = item is IEquippable;
        if (equipButton != null)
        {
            equipButton.gameObject.SetActive(isEquippable);

            if (isEquippable)
            {
                var equippable = item as IEquippable;
                bool occupied = equipment.IsOccupied(equippable.Slot);
                if (equipButtonText != null)
                    equipButtonText.text = occupied ? "Reemplazar" : "Equipar";
            }
        }

        Debug.Log($"[InventoryUI] Seleccionado: {item.ItemName} x{qty}");
    }

    private void ClearDetail()
    {
        selectedItem = null;
        if (detailPanel != null) detailPanel.SetActive(false);
    }

    private void OnEquipButtonClicked()
    {
        if (selectedItem == null || !IsOwner) return;

        int id = ItemDatabase.Instance.GetId(selectedItem);
        if (id < 0) return;

        EquipServerRpc(id);
    }

    [ServerRpc]
    private void EquipServerRpc(int itemId)
    {
        var item = ItemDatabase.Instance.Get(itemId);
        if (item is not IEquippable equippable) return;

        bool ok = equipment.Equip(equippable);
        Debug.Log($"[InventoryUI] Equipado '{item.ItemName}': {ok}");
    }
}