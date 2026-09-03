using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// UI principal del inventario. Se abre/cierra con Tab.
///
/// LAYOUT:
///   Panel izquierdo   → modelo 3D del personaje + slots de equipamiento
///                        (fijo, visible en ambas pestañas)
///   Pestaña "Objetos" → grilla de iconos de items + detalle del item
///                        seleccionado + botón equipar
///   Pestaña "Crafteo" → grilla de recetas + detalle de la receta
///                        seleccionada (ingredientes, en verde/rojo según
///                        si el jugador tiene suficiente) + botón craftear
///
/// IMPORTANTE: MonoBehaviour (no NetworkBehaviour).
/// El ownership se verifica a través del Player asignado en Initialize().
/// </summary>
public class InventoryUI : MonoBehaviour
{
    private enum Tab { Items, Crafting }

    [Header("Panel principal")]
    [SerializeField] private GameObject inventoryPanel;

    [Header("Pestañas")]
    [Tooltip("Botones para alternar entre la vista de Objetos y la de Crafteo.")]
    [SerializeField] private Button itemsTabButton;
    [SerializeField] private Button craftingTabButton;
    [Tooltip("Raíz que agrupa la grilla de items + su panel de detalle.")]
    [SerializeField] private GameObject itemsPageRoot;
    [Tooltip("Raíz que agrupa la grilla de recetas + su panel de detalle.")]
    [SerializeField] private GameObject craftingPageRoot;

    [Header("Panel izquierdo — Personaje y equipamiento")]
    [SerializeField] private RawImage characterPreview;
    [SerializeField] private Transform equipmentContainer;
    [SerializeField] private InventorySlotUI slotPrefab;

    [Header("Objetos — Grilla de items")]
    [SerializeField] private Transform itemGridContainer;
    [SerializeField] private InventoryItemUI itemCellPrefab;

    [Header("Objetos — Detalle del item")]
    [SerializeField] private Image detailIcon;
    [SerializeField] private TextMeshProUGUI detailName;
    [SerializeField] private TextMeshProUGUI detailDescription;
    [SerializeField] private TextMeshProUGUI detailQuantity;
    [SerializeField] private Button equipButton;
    [SerializeField] private TextMeshProUGUI equipButtonText;
    [SerializeField] private GameObject detailPanel;

    [Header("Crafteo — Grilla de recetas")]
    [SerializeField] private Transform recipeGridContainer;
    [SerializeField] private CraftingRecipeUI recipeCellPrefab;

    [Header("Crafteo — Detalle de la receta")]
    [SerializeField] private Image craftingIcon;
    [SerializeField] private TextMeshProUGUI craftingName;
    [SerializeField] private TextMeshProUGUI craftingDescription;
    [SerializeField] private Transform ingredientListContainer;
    [SerializeField] private TextMeshProUGUI ingredientLinePrefab;
    [SerializeField] private TextMeshProUGUI goldCostText;
    [SerializeField] private Button craftButton;
    [SerializeField] private TextMeshProUGUI craftButtonText;
    [SerializeField] private TextMeshProUGUI craftFeedbackText;
    [SerializeField] private GameObject craftingDetailPanel;

    [Header("Crafteo — Colores de ingrediente")]
    [SerializeField] private Color sufficientColor = Color.white;
    [SerializeField] private Color insufficientColor = new Color(1f, 0.35f, 0.35f);
    [SerializeField] private float feedbackDuration = 2f;

    [Header("Cámara de preview")]
    [SerializeField] private InventoryPreviewCamera previewCamera;

    // Referencia al binder de la cámara del jugador para pausar el mouse look
    private PlayerCameraBinder cameraBinder;

    private InventoryController inventory;
    private EquipmentController equipment;
    private GoldController gold;
    private Player localPlayer;

    private readonly List<InventorySlotUI> slotUIs = new();
    private readonly List<InventoryItemUI> itemUIs = new();
    private readonly List<CraftingRecipeUI> recipeUIs = new();
    private readonly List<TextMeshProUGUI> ingredientLineInstances = new();

    private ItemData selectedItem;
    private int selectedQty;

    private CraftingRecipeData selectedRecipe;
    private int selectedRecipeId = -1;

    private Tab currentTab = Tab.Items;
    private bool isOpen = false;
    private float feedbackTimer = 0f;

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

        // GoldController es opcional: si una receta no tiene costo en oro,
        // el panel de crafteo funciona igual sin él.
        this.gold = player.GetComponent<GoldController>();

        // Conectar la cámara de preview al jugador local
        previewCamera?.SetTarget(player.transform);

        // Guardar referencia al PlayerCameraBinder para poder pausar el mouse look
        cameraBinder = player.GetComponent<PlayerCameraBinder>();

        BuildEquipmentSlots();

        inventory.OnChanged += RefreshItemGrid;
        inventory.OnChanged += RefreshCraftableStates;
        equipment.OnSlotChanged += (_, __) => RefreshEquipmentSlots();
        if (gold != null) gold.OnGoldChanged += (_, __) => RefreshCraftableStates();
        player.OnCraftResult += HandleCraftResult;

        if (equipButton != null) equipButton.onClick.AddListener(OnEquipButtonClicked);
        if (craftButton != null) craftButton.onClick.AddListener(OnCraftButtonClicked);
        if (itemsTabButton != null) itemsTabButton.onClick.AddListener(() => SetTab(Tab.Items));
        if (craftingTabButton != null) craftingTabButton.onClick.AddListener(() => SetTab(Tab.Crafting));

        if (detailPanel != null) detailPanel.SetActive(false);
        if (craftingDetailPanel != null) craftingDetailPanel.SetActive(false);
        if (craftFeedbackText != null) craftFeedbackText.gameObject.SetActive(false);

        inventoryPanel?.SetActive(false);

        Debug.Log("[InventoryUI] Inicializado para jugador local.");
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnChanged -= RefreshItemGrid;
            inventory.OnChanged -= RefreshCraftableStates;
        }
        if (localPlayer != null) localPlayer.OnCraftResult -= HandleCraftResult;
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

        if (feedbackTimer > 0f)
        {
            feedbackTimer -= Time.deltaTime;
            if (feedbackTimer <= 0f && craftFeedbackText != null)
                craftFeedbackText.gameObject.SetActive(false);
        }
    }

    private void ToggleInventory()
    {
        isOpen = !isOpen;
        inventoryPanel?.SetActive(isOpen);

        if (isOpen) previewCamera?.Show();
        else previewCamera?.Hide();

        Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isOpen;

        // Congelar/liberar el mouse look de la cámara junto con el cursor
        cameraBinder?.SetLookEnabled(!isOpen);

        // FIX: bloquear también el movimiento (WASD). Antes solo se pausaba
        // el mouse look, pero el jugador seguía pudiendo caminar con el
        // inventario abierto, lo que hacía que la cámara de preview
        // (que sigue al personaje) se viera acercarse/alejarse.
        localPlayer?.SetInputBlocked(isOpen);

        if (isOpen)
        {
            RefreshEquipmentSlots();
            SetTab(Tab.Items); // siempre arranca en la pestaña de Objetos
        }

        Debug.Log($"[InventoryUI] {(isOpen ? "Abierto" : "Cerrado")}");
    }

    // =========================
    // PESTAÑAS
    // =========================

    private void SetTab(Tab tab)
    {
        currentTab = tab;

        itemsPageRoot?.SetActive(tab == Tab.Items);
        craftingPageRoot?.SetActive(tab == Tab.Crafting);

        // Limpiar ambos detalles al cambiar de pestaña evita estados
        // "fantasma" (ej: una receta seleccionada quedando activa
        // mientras se mira la pestaña de Objetos).
        ClearDetail();
        ClearCraftingDetail();

        if (tab == Tab.Items)
            RefreshItemGrid();
        else
            RefreshRecipeGrid();
    }

    // =========================
    // EQUIPAMIENTO
    // =========================

    private void BuildEquipmentSlots()
    {
        if (equipmentContainer == null) return;

        // Buscar slots precolocados como hijos del container.
        // Cada InventorySlotUI tiene su SlotType configurado en el Inspector.
        var preplacedSlots = equipmentContainer.GetComponentsInChildren<InventorySlotUI>(true);

        if (preplacedSlots.Length > 0)
        {
            foreach (var slotUI in preplacedSlots)
            {
                slotUI.SetupFromScene(equipment);
                slotUI.OnUnequipRequested += RequestUnequip;
                slotUIs.Add(slotUI);
            }
        }
        else if (slotPrefab != null)
        {
            // Fallback: instanciar dinámicamente si no hay slots precolocados
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                var slotUI = Instantiate(slotPrefab, equipmentContainer);
                slotUI.Setup(slot, equipment);
                slotUI.OnUnequipRequested += RequestUnequip;
                slotUIs.Add(slotUI);
            }
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
    // PANEL DETALLE DE ITEM
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

    // =========================
    // GRILLA DE RECETAS
    // =========================

    private void RefreshRecipeGrid()
    {
        foreach (var ui in recipeUIs) Destroy(ui.gameObject);
        recipeUIs.Clear();
        selectedRecipe = null;
        selectedRecipeId = -1;

        var db = CraftingRecipeDatabase.Instance;
        if (db == null || recipeCellPrefab == null || recipeGridContainer == null)
        {
            Debug.LogWarning("[InventoryUI] Falta CraftingRecipeDatabase.Instance o referencias de UI de crafteo.");
            return;
        }

        foreach (var recipe in db.GetAll())
        {
            if (recipe == null) continue;

            int id = db.GetId(recipe);
            var cell = Instantiate(recipeCellPrefab, recipeGridContainer);
            cell.Setup(recipe, id, CanCraft(recipe));
            cell.OnSelected += ShowCraftingDetail;
            recipeUIs.Add(cell);
        }
    }

    /// <summary>
    /// Se llama cuando cambia el inventario o el oro: re-evalúa qué recetas
    /// se pueden craftear sin reconstruir toda la grilla.
    /// </summary>
    private void RefreshCraftableStates()
    {
        foreach (var ui in recipeUIs)
            ui.SetCraftable(CanCraft(ui.Recipe));

        if (selectedRecipe != null)
            ShowCraftingDetail(selectedRecipe, selectedRecipeId);
    }

    private bool CanCraft(CraftingRecipeData recipe)
    {
        if (recipe == null || inventory == null) return false;
        int currentGold = gold != null ? gold.Gold : 0;
        return CraftingSystem.CanCraft(inventory, recipe, currentGold);
    }

    // =========================
    // PANEL DETALLE DE RECETA
    // =========================

    private void ShowCraftingDetail(CraftingRecipeData recipe, int recipeId)
    {
        selectedRecipe = recipe;
        selectedRecipeId = recipeId;

        if (craftingDetailPanel != null) craftingDetailPanel.SetActive(true);

        var output = recipe.Output;
        if (craftingIcon != null)
        {
            craftingIcon.sprite = output != null ? output.Icon : null;
            craftingIcon.enabled = output != null && output.Icon != null;
        }
        if (craftingName != null) craftingName.text = output != null ? output.ItemName : "???";
        if (craftingDescription != null) craftingDescription.text = output != null ? output.Description : "";

        BuildIngredientList(recipe);

        if (goldCostText != null)
        {
            bool hasCost = recipe.GoldCost > 0;
            goldCostText.gameObject.SetActive(hasCost);
            if (hasCost)
            {
                int have = gold != null ? gold.Gold : 0;
                goldCostText.text = $"Oro: {have} / {recipe.GoldCost}";
                goldCostText.color = have >= recipe.GoldCost ? sufficientColor : insufficientColor;
            }
        }

        bool canCraft = CanCraft(recipe);
        if (craftButton != null) craftButton.interactable = canCraft;
        if (craftButtonText != null) craftButtonText.text = "Craftear";

        foreach (var ui in recipeUIs)
            ui.SetSelected(ui.Recipe == recipe);

        Debug.Log($"[InventoryUI] Receta seleccionada: {(output != null ? output.ItemName : "???")}");
    }

    private void BuildIngredientList(CraftingRecipeData recipe)
    {
        foreach (var line in ingredientLineInstances) Destroy(line.gameObject);
        ingredientLineInstances.Clear();

        if (ingredientListContainer == null || ingredientLinePrefab == null) return;

        foreach (var ing in recipe.Ingredients)
        {
            if (ing.item == null) continue;

            int have = inventory.GetQuantity(ing.item);
            var line = Instantiate(ingredientLinePrefab, ingredientListContainer);
            line.text = $"{ing.item.ItemName}  {have} / {ing.quantity}";
            line.color = have >= ing.quantity ? sufficientColor : insufficientColor;
            ingredientLineInstances.Add(line);
        }
    }

    private void ClearCraftingDetail()
    {
        selectedRecipe = null;
        selectedRecipeId = -1;
        if (craftingDetailPanel != null) craftingDetailPanel.SetActive(false);
    }

    private void OnCraftButtonClicked()
    {
        if (selectedRecipe == null || selectedRecipeId < 0 || localPlayer == null) return;
        localPlayer.RequestCraft(selectedRecipeId);
    }

    private void HandleCraftResult(int recipeId, CraftResult result)
    {
        if (craftFeedbackText != null)
        {
            craftFeedbackText.text = result switch
            {
                CraftResult.Success => "¡Crafteado con éxito!",
                CraftResult.MissingIngredients => "Faltan materiales.",
                CraftResult.MissingGold => "No tenés suficiente oro.",
                CraftResult.InventoryFull => "Inventario lleno.",
                _ => "No se pudo craftear."
            };
            craftFeedbackText.color = result == CraftResult.Success ? sufficientColor : insufficientColor;
            craftFeedbackText.gameObject.SetActive(true);
            feedbackTimer = feedbackDuration;
        }

        // El inventario/oro ya se sincronizan solos vía red (InventoryNetworkSync /
        // NetworkVariable de GoldController), lo cual dispara RefreshCraftableStates
        // por su cuenta. Acá solo refrescamos el detalle por si el jugador sigue
        // con la misma receta seleccionada (ej: quiere craftear una segunda vez).
        if (selectedRecipe != null)
            ShowCraftingDetail(selectedRecipe, selectedRecipeId);
    }
}
