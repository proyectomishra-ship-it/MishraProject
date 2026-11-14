using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reacomoda en runtime los paneles de detalle (item y receta) del inventario con
/// un layout limpio y proporcional: icono enmarcado arriba, nombre, separadores,
/// estadisticas / ingredientes y boton al pie. Reemplaza las posiciones absolutas
/// de la escena sin editarla (la escena la comparte todo el equipo).
/// Es idempotente: se puede llamar varias veces.
/// </summary>
public static class InventoryDetailLayout
{
    private const float HeaderInset = 64f;   // deja libre el titulo "DETALLE"
    private const float Side = 22f;
    private const float IconSize = 120f;

    public class ItemRefs
    {
        public RectTransform panel;
        public Image icon;
        public TextMeshProUGUI name, quantity, stats, description;
        public Button button;
    }

    public class CraftRefs
    {
        public RectTransform panel;
        public Image icon;
        public TextMeshProUGUI name, description, gold, feedback;
        public RectTransform ingredients;
        public Button button;
    }

    // =========================
    // ITEM
    // =========================

    public static void ApplyItem(ItemRefs r)
    {
        if (r == null || r.panel == null) return;
        Stretch(r.panel);

        float y = HeaderInset;
        PlaceIcon(r.icon, ref y);

        PlaceText(r.name, ref y, 46f, 34, FontStyles.Bold, TextAlignmentOptions.Center, true);
        PlaceText(r.quantity, ref y, 28f, 20, FontStyles.Normal, TextAlignmentOptions.Center, false, Muted());
        y += 4f;
        Divider(r.panel, "DividerTop", y); y += 12f;

        // Estadisticas en columnas: nombre a la izquierda, valor a 62 % del ancho.
        PlaceText(r.stats, ref y, 200f, 24, FontStyles.Normal, TextAlignmentOptions.TopLeft, false, Primary());
        if (r.stats != null) r.stats.lineSpacing = 8f;

        Divider(r.panel, "DividerBottom", y); y += 12f;
        PlaceText(r.description, ref y, 160f, 21, FontStyles.Italic, TextAlignmentOptions.TopLeft, false, Muted());

        PlaceButton(r.button);
    }

    // =========================
    // RECETA
    // =========================

    public static void ApplyCraft(CraftRefs r)
    {
        if (r == null || r.panel == null) return;
        Stretch(r.panel);

        float y = HeaderInset;
        PlaceIcon(r.icon, ref y);

        PlaceText(r.name, ref y, 46f, 34, FontStyles.Bold, TextAlignmentOptions.Center, true);
        PlaceText(r.description, ref y, 70f, 20, FontStyles.Italic, TextAlignmentOptions.Top, false, Muted());
        Divider(r.panel, "DividerTop", y); y += 10f;

        var header = EnsureLabel(r.panel, "IngredientsHeader", "INGREDIENTES");
        PlaceText(header, ref y, 30f, 19, FontStyles.Bold, TextAlignmentOptions.Left, false, Accent());
        header.characterSpacing = 3f;

        if (r.ingredients != null)
        {
            // Ocupa el hueco entre el encabezado y el pie (feedback + oro + boton).
            r.ingredients.anchorMin = Vector2.zero;
            r.ingredients.anchorMax = Vector2.one;
            r.ingredients.pivot = new Vector2(0.5f, 1f);
            r.ingredients.offsetMin = new Vector2(Side, 156f);
            r.ingredients.offsetMax = new Vector2(-Side, -y);

            var group = r.ingredients.GetComponent<VerticalLayoutGroup>();
            if (group == null)
            {
                group = r.ingredients.gameObject.AddComponent<VerticalLayoutGroup>();
                group.childControlWidth = true;
                group.childControlHeight = true;
                group.childForceExpandWidth = true;
                group.childForceExpandHeight = false;
            }
            // Con un grupo ya existente solo se ajusta la alineacion y el espaciado.
            group.childAlignment = TextAnchor.UpperLeft;
            group.spacing = 6f;
        }

        // Pie del panel: feedback, costo en oro y boton.
        PlaceFromBottom(r.feedback, 118f, 28f, 20, TextAlignmentOptions.Center);
        PlaceFromBottom(r.gold, 86f, 30f, 22, TextAlignmentOptions.Center);
        PlaceButton(r.button);
    }

    // =========================
    // PIEZAS
    // =========================

    private static void PlaceIcon(Image icon, ref float y)
    {
        if (icon == null) return;
        var rt = icon.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y);
        rt.sizeDelta = new Vector2(IconSize, IconSize);
        icon.preserveAspect = true;
        y += IconSize + 14f;
    }

    private static void PlaceText(TMP_Text t, ref float y, float height, float size, FontStyles style,
                                  TextAlignmentOptions align, bool autoSize, Color? color = null)
    {
        if (t == null) return;
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y);
        rt.sizeDelta = new Vector2(-2f * Side, height);

        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        t.enableAutoSizing = autoSize;
        if (autoSize) { t.fontSizeMin = 22f; t.fontSizeMax = size; }
        if (color.HasValue) t.color = color.Value;

        y += height + 6f;
    }

    private static void PlaceFromBottom(TMP_Text t, float bottom, float height, float size, TextAlignmentOptions align)
    {
        if (t == null) return;
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, bottom);
        rt.sizeDelta = new Vector2(-2f * Side, height);
        t.fontSize = size;
        t.alignment = align;
        t.enableAutoSizing = false;
    }

    private static void PlaceButton(Button b)
    {
        if (b == null) return;
        var rt = (RectTransform)b.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 26f);
        rt.sizeDelta = new Vector2(240f, 58f);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Divider(RectTransform panel, string name, float y)
    {
        var existing = panel.Find(name);
        RectTransform rt;
        if (existing != null) rt = (RectTransform)existing;
        else
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rt = (RectTransform)go.transform;
            rt.SetParent(panel, false);
            go.GetComponent<Image>().raycastTarget = false;
        }

        var theme = UITheme.Current;
        Color c = theme != null ? theme.bronze : new Color(0.66f, 0.47f, 0.23f);
        c.a = 0.7f;
        rt.GetComponent<Image>().color = c;

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y);
        rt.sizeDelta = new Vector2(-2f * Side, 1f);
    }

    private static TextMeshProUGUI EnsureLabel(RectTransform panel, string name, string text)
    {
        var existing = panel.Find(name);
        TextMeshProUGUI t;
        if (existing != null) t = existing.GetComponent<TextMeshProUGUI>();
        else
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(panel, false);
            t = go.AddComponent<TextMeshProUGUI>();
            var theme = UITheme.Current;
            if (theme != null && theme.body.font != null) t.font = theme.body.font;
            t.raycastTarget = false;
        }
        t.text = text;
        return t;
    }

    private static Color Primary() => UITheme.Current != null ? UITheme.Current.textPrimary : Color.white;
    private static Color Muted() => UITheme.Current != null ? UITheme.Current.textMuted : Color.gray;
    private static Color Accent() => UITheme.Current != null ? UITheme.Current.textAccent : Color.yellow;
}
