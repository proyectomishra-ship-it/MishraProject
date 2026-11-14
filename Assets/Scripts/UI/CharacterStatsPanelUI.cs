using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantalla de estadisticas del personaje (reemplaza la camara de preview).
/// Se construye en runtime con el UITheme: cabecera (clase, nivel, experiencia)
/// y secciones de filas "nombre ..... valor". Si el contenido no entra, se
/// desplaza con la rueda del mouse. Se refresca por eventos (stats sincronizados,
/// nivel, experiencia), nunca por frame.
/// </summary>
public class CharacterStatsPanelUI : MonoBehaviour
{
    private const float RowHeight = 34f;
    private const int RowFont = 24;
    private const int SectionFont = 20;
    private const float TopInset = 78f;     // deja libre el titulo "EQUIPAMIENTO"
    private const float SideInset = 16f;
    private const float BottomInset = 16f;
    private const float FallbackSlotColumn = 120f;

    private readonly Dictionary<StatType, TextMeshProUGUI> statValues = new();
    private TextMeshProUGUI headerName, headerLevel, headerXp;
    private TextMeshProUGUI healthValue, manaValue, staminaValue;

    private CharacterStatsSyncController sync;
    private Player player;
    private bool built;

    private RectTransform slotsContainer;

    // Oculta el panel hasta que el layout del inventario ya esta calculado.
    private CanvasGroup group;
    private Coroutine positionRoutine;

    /// <summary>
    /// Crea el panel dentro del panel izquierdo (el padre del viejo preview), a la
    /// derecha de la columna de slots y debajo del titulo, ocupando todo el espacio
    /// libre. Si no hay preview del que tomar el padre, no hace nada.
    /// </summary>
    public static CharacterStatsPanelUI Create(RectTransform area, Transform slots, Player player)
    {
        if (area == null || player == null) return null;

        var go = new GameObject("CharacterStatsPanel", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(area.parent, false);
        rt.SetSiblingIndex(area.GetSiblingIndex());
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);

        var panel = go.AddComponent<CharacterStatsPanelUI>();
        panel.slotsContainer = slots as RectTransform;
        panel.Bind(player);
        panel.PositionNextToSlots();
        return panel;
    }

    /// <summary>Deja el panel a la derecha de la columna de slots, sin pisar el titulo.</summary>
    private void PositionNextToSlots()
    {
        var rt = (RectTransform)transform;
        var parent = rt.parent as RectTransform;

        float left = FallbackSlotColumn;
        if (parent != null && slotsContainer != null)
        {
            // Borde derecho del slot mas a la derecha, en coordenadas del panel izquierdo.
            float maxX = float.NegativeInfinity;
            var corners = new Vector3[4];
            foreach (var slot in slotsContainer.GetComponentsInChildren<InventorySlotUI>(true))
            {
                ((RectTransform)slot.transform).GetWorldCorners(corners);
                float x = parent.InverseTransformPoint(corners[2]).x - parent.rect.xMin;
                if (x > maxX) maxX = x;
            }
            if (!float.IsNegativeInfinity(maxX) && maxX > 20f && maxX < parent.rect.width * 0.6f)
                left = maxX + SideInset;
        }

        rt.offsetMin = new Vector2(left, BottomInset);
        rt.offsetMax = new Vector2(-SideInset, -TopInset);
    }

    private void Bind(Player p)
    {
        player = p;
        sync = p.GetComponent<CharacterStatsSyncController>();
        Build();

        if (sync != null)
        {
            sync.OnStatsSynced += Refresh;
            sync.NetLevel.OnValueChanged += OnIntChanged;
            sync.NetXP.OnValueChanged += OnIntChanged;
            sync.NetXPRequired.OnValueChanged += OnIntChanged;
            sync.NetHealth.OnValueChanged += OnFloatChanged;
            sync.NetMana.OnValueChanged += OnFloatChanged;
            sync.NetStamina.OnValueChanged += OnFloatChanged;
        }

        Refresh();
    }

    private void OnIntChanged(int previous, int current) => Refresh();
    private void OnFloatChanged(float previous, float current) => Refresh();

    private void OnEnable()
    {
        Refresh();

        // Al abrir el inventario por primera vez, Unity todavia no calculo los layouts:
        // se espera un frame y recien ahi se mide. Mientras tanto el panel queda invisible.
        if (group != null) group.alpha = 0f;

        if (positionRoutine != null) StopCoroutine(positionRoutine);
        positionRoutine = StartCoroutine(PositionWhenLayoutReady());
    }

    private void OnDisable()
    {
        positionRoutine = null;   // Unity ya detiene la corrutina al desactivar el objeto
    }

    private IEnumerator PositionWhenLayoutReady()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        PositionNextToSlots();

        if (group != null) group.alpha = 1f;
        positionRoutine = null;
    }

    private void OnDestroy()
    {
        if (sync == null) return;

        sync.OnStatsSynced -= Refresh;
        sync.NetLevel.OnValueChanged -= OnIntChanged;
        sync.NetXP.OnValueChanged -= OnIntChanged;
        sync.NetXPRequired.OnValueChanged -= OnIntChanged;
        sync.NetHealth.OnValueChanged -= OnFloatChanged;
        sync.NetMana.OnValueChanged -= OnFloatChanged;
        sync.NetStamina.OnValueChanged -= OnFloatChanged;
    }

    // =========================
    // CONSTRUCCION
    // =========================

    private void Build()
    {
        if (built) return;
        built = true;

        group = gameObject.AddComponent<CanvasGroup>();

        var theme = UITheme.Current;
        Color panelColor = theme != null ? theme.stoneDark : new Color(0.1f, 0.09f, 0.08f);

        var bg = gameObject.AddComponent<Image>();
        bg.color = new Color(panelColor.r, panelColor.g, panelColor.b, 0.55f);
        bg.raycastTarget = true;

        var border = gameObject.AddComponent<Outline>();
        Color bc = theme != null ? theme.bronze : new Color(0.66f, 0.47f, 0.23f);
        border.effectColor = new Color(bc.r, bc.g, bc.b, 0.7f);
        border.effectDistance = new Vector2(1.5f, 1.5f);

        // Viewport + contenido desplazable.
        var viewport = NewRect("Viewport", transform);
        Stretch(viewport, 10f);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 3f;
        layout.padding = new RectOffset(10, 10, 8, 12);

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        // ---- Cabecera ----
        headerName = AddText(content, "", 34, FontStyles.Bold, AccentColor(), TextAlignmentOptions.Left, 44f);
        var levelRow = NewRow(content, 32f);
        headerLevel = AddCell(levelRow, "", RowFont, MutedColor(), TextAlignmentOptions.Left);
        headerXp = AddCell(levelRow, "", RowFont, MutedColor(), TextAlignmentOptions.Right);
        AddDivider(content);

        // ---- Secciones ----
        // Los acentos van como secuencias \u para que no dependan de la codificacion del archivo.
        AddSection(content, "Recursos");
        healthValue = AddStatRow(content, "Vida");
        manaValue = AddStatRow(content, "Man\u00e1");
        staminaValue = AddStatRow(content, "Stamina");

        AddSection(content, "Combate");
        AddStat(content, "Ataque", StatType.Attack);
        AddStat(content, "Alcance de ataque", StatType.AttackRange);
        AddStat(content, "Defensa", StatType.Defense);
        AddStat(content, "Prob. cr\u00edtica", StatType.CriticalChance);

        AddSection(content, "Atributos");
        AddStat(content, "Vitalidad", StatType.Vitality);
        AddStat(content, "Inteligencia", StatType.Intelligence);
        AddStat(content, "Destreza", StatType.Dexterity);
        AddStat(content, "Agilidad", StatType.Agility);
        AddStat(content, "Suerte", StatType.Luck);

        AddSection(content, "Movimiento");
        AddStat(content, "Velocidad", StatType.Speed);
    }

    private void AddStat(RectTransform parent, string label, StatType type)
        => statValues[type] = AddStatRow(parent, label);

    private TextMeshProUGUI AddStatRow(RectTransform parent, string label)
    {
        var row = NewRow(parent, RowHeight);
        AddCell(row, label, RowFont, MutedColor(), TextAlignmentOptions.Left);
        return AddCell(row, "-", RowFont, PrimaryColor(), TextAlignmentOptions.Right);
    }

    private void AddSection(RectTransform parent, string title)
    {
        var spacer = NewRect("Spacer", parent);
        spacer.gameObject.AddComponent<LayoutElement>().preferredHeight = 12f;

        AddText(parent, title.ToUpperInvariant(), SectionFont, FontStyles.Bold, AccentColor(),
                TextAlignmentOptions.Left, 30f, 2f);
        AddDivider(parent);
    }

    private void AddDivider(RectTransform parent)
    {
        var rt = NewRect("Divider", parent);
        var img = rt.gameObject.AddComponent<Image>();
        var theme = UITheme.Current;
        Color c = theme != null ? theme.bronze : new Color(0.66f, 0.47f, 0.23f);
        c.a = 0.6f;
        img.color = c;
        img.raycastTarget = false;
        rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 1f;
    }

    private RectTransform NewRow(RectTransform parent, float height)
    {
        var rt = NewRect("Row", parent);
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;
        h.padding = new RectOffset(4, 4, 0, 0);
        rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return rt;
    }

    private TextMeshProUGUI AddCell(RectTransform row, string text, int size, Color color, TextAlignmentOptions align)
    {
        var rt = NewRect("Cell", row);
        var t = SetupText(rt.gameObject, text, size, FontStyles.Normal, color, align);
        rt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        return t;
    }

    private TextMeshProUGUI AddText(RectTransform parent, string text, int size, FontStyles style,
                                    Color color, TextAlignmentOptions align, float height, float spacing = 0f)
    {
        var rt = NewRect("Text", parent);
        var t = SetupText(rt.gameObject, text, size, style, color, align);
        t.characterSpacing = spacing;
        rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return t;
    }

    private static TextMeshProUGUI SetupText(GameObject go, string text, int size, FontStyles style,
                                             Color color, TextAlignmentOptions align)
    {
        var t = go.AddComponent<TextMeshProUGUI>();
        var theme = UITheme.Current;
        if (theme != null && theme.body.font != null) t.font = theme.body.font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        return t;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static void Stretch(RectTransform rt, float margin)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin);
        rt.offsetMax = new Vector2(-margin, -margin);
    }

    private static Color PrimaryColor() => UITheme.Current != null ? UITheme.Current.textPrimary : Color.white;
    private static Color MutedColor() => UITheme.Current != null ? UITheme.Current.textMuted : Color.gray;
    private static Color AccentColor() => UITheme.Current != null ? UITheme.Current.textAccent : Color.yellow;

    // =========================
    // REFRESCO
    // =========================

    public void Refresh()
    {
        // headerName == null tambien cubre el caso de un panel ya destruido.
        if (!built || headerName == null || player == null || sync == null) return;

        string className = player.ClassData != null ? player.ClassData.DisplayName : "Aventurero";
        headerName.text = className;
        headerLevel.text = $"Nivel {sync.NetLevel.Value}";
        headerXp.text = sync.NetXPRequired.Value > 0
            ? $"Experiencia {sync.NetXP.Value} / {sync.NetXPRequired.Value}"
            : "";

        healthValue.text = $"{sync.NetHealth.Value:0} / {sync.GetStatValue(StatType.MaxHealth):0}";
        manaValue.text = $"{sync.NetMana.Value:0} / {sync.GetStatValue(StatType.MaxMana):0}";
        staminaValue.text = $"{sync.NetStamina.Value:0} / {sync.GetStatValue(StatType.Stamina):0}";

        foreach (var kv in statValues)
            kv.Value.text = sync.GetStatValue(kv.Key).ToString("0.##");
    }
}