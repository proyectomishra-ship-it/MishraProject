using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// UI principal del inventario. Se abre/cierra con Tab.
///
/// LAYOUT:
///   Panel izquierdo  → modelo 3D del personaje + slots de equipamiento
///   Panel central    → grilla de iconos de items
///   Panel derecho    → detalle del item seleccionado + boton equipar
///
/// IMPORTANTE: MonoBehaviour (no NetworkBehaviour).
/// El ownership se verifica a través del Player asignado en Initialize().
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Panel principal")]
    [SerializeField] private GameObject inventoryPanel;

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

    [Header("Cámara de preview")]
    [SerializeField] private InventoryPreviewCamera previewCamera;

    private InventoryController inventory;
    private EquipmentController equipment;
    private Player localPlayer;

    private readonly List<InventorySlotUI> slotUIs = new();
    private readonly List<InventoryItemUI> itemUIs = new();

    private ItemData selectedItem;
    private int selectedQty;
    private bool isOpen = false;

    // =========================
    // INIT
    // =========================

    /// <summary>
    /// Llamar desde Player.OnNetworkSpawn() solo para el owner local.
    /// </summary>
    public void Initialize(
        InventoryController inventory,
        EquipmentController equipment,
        Player player)
    {
        this.inventory = inventory;
        this.equipment = equipment;
        this.localPlayer = player;

        // Conectar la cámara de preview al jugador local
        previewCamera?.SetTarget(player.transform);

        BuildEquipmentSlots();

        inventory.OnChanged += RefreshItemGrid;
        equipment.OnSlotChanged += (_, __) => RefreshEquipmentSlots();

        if (equipButton != null) equipButton.onClick.AddListener(OnEquipButtonClicked);
        if (detailPanel != null) detailPanel.SetActive(false);

        inventoryPanel?.SetActive(false);

        Debug.Log("[InventoryUI] Inicializado para jugador local.");
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
        // Solo responde si fue inicializado para el jugador local
        if (localPlayer == null) return;

        if (Keyboard.current.tabKey.wasPressedThisFrame)
            ToggleInventory();
    }

    private void ToggleInventory()
    {
        isOpen = !isOpen;
        inventoryPanel?.SetActive(isOpen);

        if (isOpen) previewCamera?.Show();
        else previewCamera?.Hide();

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
        localPlayer?.RequestUnequip(slot);
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

        if (detailPanel != null) detailPanel.SetActive(true);
        if (detailIcon != null)
        {
            detailIcon.sprite = item.Icon;
            detailIcon.enabled = item.Icon != null;
        }
        if (detailName != null) detailName.text = item.ItemName;
        if (detailDescription != null) detailDescription.text = item.Description;
        if (detailQuantity != null) detailQuantity.text = qty > 1 ? $"Cantidad: {qty}" : "";

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
        if (selectedItem == null || localPlayer == null) return;

        int id = ItemDatabase.Instance.GetId(selectedItem);
        if (id < 0) return;

        localPlayer.RequestEquip(id);
    }
}