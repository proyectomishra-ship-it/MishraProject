using System.Collections.Generic;
using System.Text;
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

    [Header("HUD a ocultar con el inventario abierto")]
    [Tooltip("Objetos que se apagan al abrir el inventario (misiones, oro, etc.).")]
    [SerializeField] private GameObject[] hudToHideWhenOpen;

    [Header("Pestañas")]
    [Tooltip("Botones para alternar entre la vista de Objetos y la de Crafteo.")]
    [SerializeField] private Button itemsTabButton;
    [SerializeField] private Button craftingTabButton;
    [Tooltip("Hijo de PanelCenter: agrupa LabelItems + ItemScrollView (lo que ya existía).")]
    [SerializeField] private GameObject itemsGridRoot;
    [Tooltip("Hijo de PanelCenter, hermano de Items Grid Root: contiene la grilla de recetas nueva.")]
    [SerializeField] private GameObject craftingGridRoot;

    [Header("Panel izquierdo — Personaje y equipamiento")]
    [SerializeField] private RawImage characterPreview;
    [SerializeField] private Transform equipmentContainer;
    [SerializeField] private InventorySlotUI slotPrefab;

    [System.Serializable]
    public struct SlotPlaceholder
    {
        public EquipmentSlot slot;
        public Sprite sprite;
    }

    [Tooltip("Silueta que se muestra en cada slot de equipamiento mientras esta vacio. " +
             "Si un tipo no esta en la lista, ese slot muestra su etiqueta de texto.")]
    [SerializeField] private List<SlotPlaceholder> slotPlaceholders = new();

    [Header("Objetos — Grilla de items")]
    [SerializeField] private Transform itemGridContainer;
    [SerializeField] private InventoryItemUI itemCellPrefab;

    [Header("Objetos — Detalle del item")]
    [SerializeField] private Image detailIcon;
    [SerializeField] private TextMeshProUGUI detailName;
    [Tooltip("Opcional: texto con las estadisticas, ubicado debajo del nombre. " +
             "Si queda vacio, las estadisticas se muestran al inicio de la descripcion.")]
    [SerializeField] private TextMeshProUGUI detailStats;
    [SerializeField] private TextMeshProUGUI detailDescription;
    [SerializeField] private TextMeshProUGUI detailQuantity;
    [SerializeField] private Button equipButton;
    [SerializeField] private TextMeshProUGUI equipButtonText;
    [SerializeField] private GameObject detailPanel;
    [Tooltip("Opcional: mensaje al usar un consumible (ej: 'Vida al máximo').")]
    [SerializeField] private TextMeshProUGUI itemFeedbackText;

    [Header("Crafteo — Grilla de recetas")]
    [SerializeField] private Transform recipeGridContainer;
    [SerializeField] private CraftingRecipeUI recipeCellPrefab;

    [Header("Crafteo — Detalle de la receta")]
    [SerializeField] private Image craftingIcon;
    [SerializeField] private TextMeshProUGUI craftingName;
    [SerializeField] private TextMeshProUGUI craftingDescription;
    [SerializeField] private Transform ingredientListContainer;
    [SerializeField] private TextMeshProUGUI ingredientLinePrefab;
    [Tooltip("Opcional: línea con icono. Si se asigna, reemplaza a Ingredient Line Prefab.")]
    [SerializeField] private IngredientLineUI ingredientLineUIPrefab;
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
    private readonly List<GameObject> ingredientLineInstances = new();

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

        // Los iconos de detalle nunca deben deformarse, sin importar
        // lo que haya guardado la escena.
        if (detailIcon != null) detailIcon.preserveAspect = true;
        if (craftingIcon != null) craftingIcon.preserveAspect = true;

        // Un solo refresco por frame: la NetworkList del cliente dispara N+1
        // eventos por cada cambio (Clear + un Add por slot). Ver LateUpdate().
        inventory.OnChanged += QueueRefresh;
        equipment.OnSlotChanged += (_, __) => RefreshEquipmentSlots();
        if (gold != null) gold.OnGoldChanged += HandleGoldChanged;
        player.OnCraftResult += HandleCraftResult;
        player.OnConsumeResult += HandleConsumeResult;

        // RemoveAllListeners antes de AddListener: si Initialize se llama otra vez
        // sobre esta misma UI (reconexion, cambio de personaje sin recargar la
        // escena), los botones no acumulan listeners duplicados. Sin esto,
        // "Craftear" mandaria dos pedidos por cada clic.
        // Solo quita listeners agregados por codigo; los asignados en el
        // Inspector (persistentes) se conservan.
        if (equipButton != null)
        {
            equipButton.onClick.RemoveAllListeners();
            equipButton.onClick.AddListener(OnEquipButtonClicked);
        }
        if (craftButton != null)
        {
            craftButton.onClick.RemoveAllListeners();
            craftButton.onClick.AddListener(OnCraftButtonClicked);
        }
        if (itemsTabButton != null)
        {
            itemsTabButton.onClick.RemoveAllListeners();
            itemsTabButton.onClick.AddListener(() => SetTab(Tab.Items));
        }
        if (craftingTabButton != null)
        {
            craftingTabButton.onClick.RemoveAllListeners();
            craftingTabButton.onClick.AddListener(() => SetTab(Tab.Crafting));
        }

        if (detailPanel != null) detailPanel.SetActive(false);
        if (craftingDetailPanel != null) craftingDetailPanel.SetActive(false);
        if (craftFeedbackText != null) craftFeedbackText.gameObject.SetActive(false);

        inventoryPanel?.SetActive(false);

        Debug.Log("[InventoryUI] Inicializado para jugador local.");
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnChanged -= QueueRefresh;
        if (gold != null)
            gold.OnGoldChanged -= HandleGoldChanged;
        if (localPlayer != null)
        {
            localPlayer.OnCraftResult -= HandleCraftResult;
            localPlayer.OnConsumeResult -= HandleConsumeResult;
        }
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // DEBUG: F9 con el inventario abierto = +5 de cada material.
        if (isOpen && Keyboard.current.f9Key.wasPressedThisFrame)
            localPlayer.DebugRequestMaterials();
#endif

        if (feedbackTimer > 0f)
        {
            feedbackTimer -= Time.deltaTime;
            if (feedbackTimer <= 0f)
            {
                if (craftFeedbackText != null) craftFeedbackText.gameObject.SetActive(false);
                if (itemFeedbackText != null) itemFeedbackText.gameObject.SetActive(false);
            }
        }
    }

    // =========================
    // REFRESCO DIFERIDO (debounce)
    // =========================

    private bool refreshQueued;

    private void QueueRefresh() => refreshQueued = true;
    private void HandleGoldChanged(int _, int __) => QueueRefresh();

    /// <summary>
    /// Agrupa todos los cambios de inventario/oro de un mismo frame en un
    /// único refresco. Evita reconstruir la grilla N veces y el parpadeo del
    /// estado intermedio (inventario "vacío" tras el Clear de la NetworkList).
    /// Con el panel cerrado no hace nada: al abrirlo, SetTab() ya refresca.
    /// </summary>
    private void LateUpdate()
    {
        if (!refreshQueued) return;
        refreshQueued = false;
        if (!isOpen) return;

        if (currentTab == Tab.Items) RefreshItemGrid();
        else RefreshCraftableStates();
    }

    private void ToggleInventory()
    {
        isOpen = !isOpen;
        inventoryPanel?.SetActive(isOpen);
        SetHudVisible(!isOpen);

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

    /// <summary>
    /// Muestra u oculta el HUD (misiones, oro, etc.) segun el inventario
    /// este cerrado o abierto.
    /// </summary>
    private void SetHudVisible(bool visible)
    {
        if (hudToHideWhenOpen == null) return;

        foreach (var go in hudToHideWhenOpen)
            if (go != null) go.SetActive(visible);
    }

    // =========================
    // PESTAÑAS
    // =========================

    private void SetTab(Tab tab)
    {
        currentTab = tab;

        itemsGridRoot?.SetActive(tab == Tab.Items);
        craftingGridRoot?.SetActive(tab == Tab.Crafting);

        // Limpiar ambos detalles al cambiar de pestaña evita estados
        // "fantasma" (ej: una receta seleccionada quedando activa
        // mientras se mira la pestaña de Objetos). ClearDetail/
        // ClearCraftingDetail ya llaman a UpdateDetailVisibility().
        ClearDetail();
        ClearCraftingDetail();

        if (tab == Tab.Items)
            RefreshItemGrid();
        else
            RefreshRecipeGrid();
    }

    /// <summary>
    /// detailPanel y craftingDetailPanel pueden colgar directamente de
    /// PanelRight como hermanos (no hace falta envolverlos en una raíz por
    /// pestaña): cada uno se muestra solo si SU pestaña está activa Y hay
    /// algo seleccionado. Se llama después de tocar currentTab, selectedItem
    /// o selectedRecipe.
    /// </summary>
    private void UpdateDetailVisibility()
    {
        detailPanel?.SetActive(currentTab == Tab.Items && selectedItem != null);
        craftingDetailPanel?.SetActive(currentTab == Tab.Crafting && selectedRecipe != null);
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
                // Nombre visible en la Hierarchy para saber cual es cada slot.
                slotUI.name = $"Slot_{slot}";
                slotUI.Setup(slot, equipment);
                slotUI.OnUnequipRequested += RequestUnequip;
                slotUIs.Add(slotUI);
            }
        }

        foreach (var slotUI in slotUIs)
            slotUI.SetPlaceholder(GetPlaceholderSprite(slotUI.Slot));
    }

    private Sprite GetPlaceholderSprite(EquipmentSlot slot)
    {
        foreach (var entry in slotPlaceholders)
            if (entry.slot == slot && entry.sprite != null)
                return entry.sprite;

        return null;
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
        // Recordar la seleccion: al reconstruir la grilla se pierde, y antes el panel
        // de detalle quedaba abierto mostrando un item que ya no estaba seleccionado.
        ItemData previous = selectedItem;

        foreach (var ui in itemUIs) Destroy(ui.gameObject);
        itemUIs.Clear();
        selectedItem = null;

        int previousQty = 0;
        foreach (var (item, qty) in inventory.GetAll())
        {
            var cell = Instantiate(itemCellPrefab, itemGridContainer);
            cell.Setup(item, qty);
            cell.OnSelected += ShowDetail;
            itemUIs.Add(cell);
            if (item == previous) previousQty = qty;
        }

        // Si el item sigue en el inventario, restaurar el detalle (cantidad y texto del
        // boton actualizados). Si ya no esta (se equipo, se tiro...), cerrar el panel.
        if (previous != null && previousQty > 0) ShowDetail(previous, previousQty);
        else UpdateDetailVisibility();
    }

    // =========================
    // PANEL DETALLE DE ITEM
    // =========================

    private void ShowDetail(ItemData item, int qty)
    {
        selectedItem = item;
        selectedQty = qty;

        if (detailIcon != null)
        {
            detailIcon.sprite = item.Icon;
            detailIcon.enabled = item.Icon != null;
        }
        if (detailName != null) detailName.text = item.ItemName;

        // Estadisticas del item (armas y equipamiento). Vacio para el resto.
        string stats = BuildItemStats(item);
        if (detailStats != null)
        {
            // Hay un texto dedicado debajo del nombre.
            detailStats.text = stats;
            detailStats.gameObject.SetActive(!string.IsNullOrEmpty(stats));
            if (detailDescription != null) detailDescription.text = item.Description;
        }
        else if (detailDescription != null)
        {
            // Sin texto dedicado: las stats van primero y la descripcion despues.
            bool hasDesc = !string.IsNullOrEmpty(item.Description);
            detailDescription.text = string.IsNullOrEmpty(stats) ? item.Description
                                   : hasDesc ? stats + "\n\n" + item.Description
                                   : stats;
        }

        if (detailQuantity != null) detailQuantity.text = qty > 1 ? $"Cantidad: {qty}" : "";

        bool isEquippable = item is IEquippable;
        bool isUsable = item.IsUsableConsumable;
        if (equipButton != null)
        {
            // El mismo botón sirve para Equipar (armas/armaduras) y Usar (consumibles).
            equipButton.gameObject.SetActive(isEquippable || isUsable);

            if (isUsable)
            {
                if (equipButtonText != null) equipButtonText.text = "Usar";
            }
            else if (isEquippable)
            {
                var equippable = item as IEquippable;
                bool occupied = equipment.IsOccupied(equippable.Slot);
                if (equipButtonText != null)
                    equipButtonText.text = occupied ? "Reemplazar" : "Equipar";
            }
        }

        UpdateDetailVisibility();

        Debug.Log($"[InventoryUI] Seleccionado: {item.ItemName} x{qty}");
    }

    private void ClearDetail()
    {
        selectedItem = null;
        UpdateDetailVisibility();
    }

    // =========================
    // ESTADISTICAS DEL ITEM
    // =========================

    /// <summary>
    /// Texto de efectos de un consumible (ej: "Restaura 50 de vida").
    /// </summary>
    private static string BuildConsumableStats(ItemData item)
    {
        var sb = new StringBuilder();
        if (item.HealthRestore > 0f) AppendLine(sb, $"Restaura {item.HealthRestore:0} de vida");
        if (item.ManaRestore > 0f) AppendLine(sb, $"Restaura {item.ManaRestore:0} de maná");
        return sb.ToString();
    }

    /// <summary>
    /// Arma el texto de estadisticas basicas de un item equipable (arma o armadura):
    /// solo sus modificadores de stats (Ataque, Rango de ataque, etc.).
    /// Para consumibles devuelve sus efectos. Devuelve "" si el item no es ninguno de los dos.
    /// </summary>
    private static string BuildItemStats(ItemData item)
    {
        if (item.IsUsableConsumable) return BuildConsumableStats(item);

        if (item is not IEquippable equippable) return "";

        var sb = new StringBuilder();
        AppendModifiers(sb, equippable.Modifiers);

        return sb.ToString().TrimEnd('\n');
    }

    private static void AppendModifiers(StringBuilder sb, List<StatModifier> modifiers)
    {
        if (modifiers == null) return;

        foreach (var mod in modifiers)
        {
            if (mod == null) continue;
            string sign = mod.value >= 0f ? "+" : "";
            AppendLine(sb, $"{StatLabel(mod.stat)}: {sign}{Fmt(mod.value)}");
        }
    }

    // '\n' fijo (en vez de AppendLine) para que TextMeshPro lo trate igual en todas las plataformas.
    private static void AppendLine(StringBuilder sb, string text) => sb.Append(text).Append('\n');

    private static string Fmt(float value) => value.ToString("0.##");

    private static string StatLabel(StatType stat) => stat switch
    {
        StatType.Attack => "Ataque",
        StatType.AttackRange => "Rango de ataque",
        StatType.Defense => "Defensa",
        StatType.MaxHealth => "Vida máxima",
        StatType.MaxMana => "Maná máximo",
        StatType.Speed => "Velocidad",
        StatType.Agility => "Agilidad",
        StatType.CriticalChance => "Prob. crítica",
        StatType.Dexterity => "Destreza",
        StatType.Intelligence => "Inteligencia",
        StatType.Vitality => "Vitalidad",
        StatType.Stamina => "Stamina",
        StatType.Luck => "Suerte",
        _ => stat.ToString()
    };

    private void OnEquipButtonClicked()
    {
        if (selectedItem == null || localPlayer == null) return;

        int id = ItemDatabase.Instance.GetId(selectedItem);
        if (id < 0) return;

        if (selectedItem.IsUsableConsumable)
        {
            localPlayer.RequestUseItem(id);
            return;
        }

        localPlayer.RequestEquip(id);
    }

    private void HandleConsumeResult(ConsumeResult result)
    {
        string msg = result switch
        {
            ConsumeResult.Success => "¡Usado!",
            ConsumeResult.NothingToRestore => "Ya estás al máximo.",
            ConsumeResult.PlayerDead => "No puedes usar objetos estando muerto.",
            ConsumeResult.NotInInventory => "Ya no tienes ese objeto.",
            _ => "No se puede usar."
        };

        if (itemFeedbackText != null)
        {
            itemFeedbackText.text = msg;
            itemFeedbackText.color = result == ConsumeResult.Success ? sufficientColor : insufficientColor;
            itemFeedbackText.gameObject.SetActive(true);
            feedbackTimer = feedbackDuration;
        }
        else
        {
            Debug.Log($"[InventoryUI] Uso de consumible: {result}");
        }
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

        UpdateDetailVisibility();

        Debug.Log($"[InventoryUI] Receta seleccionada: {(output != null ? output.ItemName : "???")}");
    }

    private void BuildIngredientList(CraftingRecipeData recipe)
    {
        foreach (var line in ingredientLineInstances) Destroy(line);
        ingredientLineInstances.Clear();

        if (ingredientListContainer == null) return;
        if (ingredientLineUIPrefab == null && ingredientLinePrefab == null) return;

        foreach (var ing in recipe.Ingredients)
        {
            if (ing.item == null) continue;

            int have = inventory.GetQuantity(ing.item);
            Color color = have >= ing.quantity ? sufficientColor : insufficientColor;

            if (ingredientLineUIPrefab != null)
            {
                // Línea con icono
                var lineUI = Instantiate(ingredientLineUIPrefab, ingredientListContainer);
                lineUI.Setup(ing.item, have, ing.quantity, color);
                ingredientLineInstances.Add(lineUI.gameObject);
            }
            else
            {
                // Fallback: solo texto (comportamiento anterior)
                var line = Instantiate(ingredientLinePrefab, ingredientListContainer);
                line.text = $"{ing.item.ItemName}  {have} / {ing.quantity}";
                line.color = color;
                ingredientLineInstances.Add(line.gameObject);
            }
        }
    }

    private void ClearCraftingDetail()
    {
        selectedRecipe = null;
        selectedRecipeId = -1;
        UpdateDetailVisibility();
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