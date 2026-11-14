using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Marco de rareza para slots y celdas: un borde fino del color de la rareza
/// mas un tinte muy suave en el fondo. Se construye solo (sin tocar prefabs) como
/// hijo "RarityFrame" del slot, detras del icono. Estetica sobria: lineas planas,
/// nada de brillos ni animaciones.
/// </summary>
public static class RarityFrame
{
    private const string FrameName = "RarityFrame";
    private const float TintAlpha = 0.14f;
    private const float CommonEdgeAlpha = 0.55f;

    /// <summary>Color de la rareza segun el tema actual.</summary>
    public static Color ColorOf(ItemRarity rarity)
        => UITheme.Current != null ? UITheme.Current.GetRarityColor(rarity) : Color.white;

    /// <summary>
    /// Pinta (o crea) el marco en el slot. item == null o sin rareza = oculta el marco.
    /// </summary>
    public static void Apply(Transform slot, ItemData item)
    {
        if (slot == null) return;

        var frame = slot.Find(FrameName);

        if (item == null)
        {
            if (frame != null) frame.gameObject.SetActive(false);
            return;
        }

        if (frame == null) frame = Build(slot);
        frame.gameObject.SetActive(true);

        Color c = ColorOf(item.Rarity);
        float thickness = UITheme.Current != null ? Mathf.Max(1f, UITheme.Current.borderThickness) : 2f;

        Color edge = c;
        if (item.Rarity == ItemRarity.Common) edge.a = CommonEdgeAlpha;
        Color tint = c;
        tint.a = item.Rarity == ItemRarity.Common ? 0f : TintAlpha;

        SetImage(frame.Find("Tint"), tint);
        SetEdge(frame.Find("Top"), edge, thickness);
        SetEdge(frame.Find("Bottom"), edge, thickness);
        SetEdge(frame.Find("Left"), edge, thickness);
        SetEdge(frame.Find("Right"), edge, thickness);
    }

    private static Transform Build(Transform slot)
    {
        var root = new GameObject(FrameName, typeof(RectTransform));
        var rt = (RectTransform)root.transform;
        rt.SetParent(slot, false);
        Stretch(rt);
        rt.SetAsFirstSibling(); // encima del fondo del slot, debajo del icono

        // El slot puede tener un LayoutGroup: el marco no participa del layout.
        root.AddComponent<LayoutElement>().ignoreLayout = true;

        MakeImage("Tint", rt, Stretch);
        MakeImage("Top", rt, r => Edge(r, new Vector2(0, 1), new Vector2(1, 1)));
        MakeImage("Bottom", rt, r => Edge(r, new Vector2(0, 0), new Vector2(1, 0)));
        MakeImage("Left", rt, r => Edge(r, new Vector2(0, 0), new Vector2(0, 1)));
        MakeImage("Right", rt, r => Edge(r, new Vector2(1, 0), new Vector2(1, 1)));
        return rt;
    }

    private static void MakeImage(string name, RectTransform parent, System.Action<RectTransform> layout)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        layout(rt);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Edge(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetImage(Transform t, Color color)
    {
        if (t != null && t.TryGetComponent(out Image img)) img.color = color;
    }

    private static void SetEdge(Transform t, Color color, float thickness)
    {
        if (t == null) return;
        var rt = (RectTransform)t;
        bool horizontal = rt.anchorMin.y == rt.anchorMax.y; // Top / Bottom
        rt.sizeDelta = horizontal ? new Vector2(0f, thickness) : new Vector2(thickness, 0f);
        SetImage(t, color);
    }
}
