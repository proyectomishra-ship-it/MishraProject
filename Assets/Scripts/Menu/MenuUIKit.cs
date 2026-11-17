using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Pequeño kit para armar paneles de menú por código con el estilo de UITheme
/// (mismos colores y fuente que el resto del juego). No hace falta tocar ninguna escena.
/// </summary>
public static class MenuUIKit
{
    private static UITheme T => UITheme.Current;

    // ---------- Colores (con respaldo si no hay UITheme) ----------
    public static Color StoneDark => T != null ? T.stoneDark : new Color(0.106f, 0.094f, 0.086f, 1f);
    public static Color StoneMid  => T != null ? T.stoneMid  : new Color(0.18f, 0.16f, 0.14f, 1f);
    public static Color Bronze    => T != null ? T.bronze    : new Color(0.66f, 0.47f, 0.23f, 1f);
    public static Color Gold      => T != null ? T.textAccent : new Color(0.85f, 0.69f, 0.4f, 1f);
    public static Color TextMain  => T != null ? T.textPrimary : new Color(0.91f, 0.86f, 0.75f, 1f);
    public static Color TextSoft  => T != null ? T.textMuted : new Color(0.61f, 0.57f, 0.49f, 1f);
    public static float Border    => T != null ? T.borderThickness : 2f;

    // ---------- Canvas / EventSystem ----------

    public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>Si la escena no tiene EventSystem (los botones no responderían) crea uno.</summary>
    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    // ---------- Objetos básicos ----------

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static Image NewImage(string name, Transform parent, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static LayoutElement Size(GameObject go, float width = -1f, float height = -1f, float flexibleWidth = -1f)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        if (width >= 0f) le.preferredWidth = width;
        if (height >= 0f) le.preferredHeight = height;
        if (flexibleWidth >= 0f) le.flexibleWidth = flexibleWidth;
        return le;
    }

    public static void AddBorder(GameObject go, Color color)
    {
        Outline outline = go.AddComponent<Outline>();
        outline.useGraphicAlpha = false;
        outline.effectColor = color;
        outline.effectDistance = new Vector2(Border, -Border);
    }

    public static TextMeshProUGUI NewText(string name, Transform parent, string text,
        UITheme.TextStyle style, float size, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();

        if (T != null)
        {
            UITheme.TextStyleData data = T.GetTextStyle(style);
            if (data.font != null) tmp.font = data.font;
            tmp.characterSpacing = data.characterSpacing;
            if (data.allCaps) tmp.fontStyle |= FontStyles.UpperCase;
        }

        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.text = text;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>Línea fina decorativa.</summary>
    public static void NewDivider(Transform parent, float height = 2f)
    {
        Image line = NewImage("Divider", parent, new Color(Bronze.r, Bronze.g, Bronze.b, 0.7f));
        line.raycastTarget = false;
        Size(line.gameObject, -1f, height, 1f);
    }

    public static void NewSpacer(Transform parent, float height)
    {
        RectTransform rt = NewRect("Spacer", parent);
        Size(rt.gameObject, -1f, height);
    }

    // ---------- Panel centrado con fondo oscuro detrás ----------

    /// <summary>
    /// Crea un fondo que oscurece la pantalla (y bloquea clics detrás) y un panel centrado
    /// con layout vertical. Devuelve el panel; "root" es el objeto que se activa/desactiva.
    /// </summary>
    public static RectTransform NewModal(Transform canvas, string name, float width, float height, out GameObject root)
    {
        Image dim = NewImage(name + "_Dim", canvas, new Color(0f, 0f, 0f, 0.65f));
        Stretch(dim.rectTransform);
        root = dim.gameObject;

        Color panelColor = StoneDark;
        panelColor.a = 0.98f;

        Image panel = NewImage(name, dim.transform, panelColor);
        AddBorder(panel.gameObject, Bronze);

        RectTransform rt = panel.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(48, 48, 36, 36);
        v.spacing = 14f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        return rt;
    }

    // ---------- Botón ----------

    public static Button NewButton(Transform parent, string label, float height, Action onClick, float fontSize = 28f)
    {
        RectTransform rt = NewRect(label + "_Button", parent);
        Image image = rt.gameObject.AddComponent<Image>();

        Sprite sprite = T != null ? T.GetSprite(UITheme.SpriteRole.Button) : null;
        ColorBlock colors = ColorBlock.defaultColorBlock;

        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        }
        else
        {
            image.color = Color.white;
            colors.normalColor = StoneMid;
            colors.highlightedColor = Color.Lerp(StoneMid, Bronze, 0.45f);
            colors.pressedColor = StoneDark;
        }

        colors.selectedColor = colors.normalColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;

        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = colors;
        button.onClick.AddListener(() =>
        {
            AudioManager.PlayUI(UISound.Click);
            onClick?.Invoke();
        });

        if (sprite == null) AddBorder(rt.gameObject, Bronze);

        Size(rt.gameObject, -1f, height, 1f);

        TextMeshProUGUI text = NewText("Label", rt, label, UITheme.TextStyle.Button, fontSize, TextMain, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);

        return button;
    }

    public static void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;
        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null) text.text = label;
    }

    // ---------- Slider de volumen (fila: nombre | barra | porcentaje) ----------

    public static Slider NewSliderRow(Transform parent, string label, float initial, Action<float> onChanged)
    {
        RectTransform row = NewRect(label + "_Row", parent);
        Size(row.gameObject, -1f, 46f, 1f);

        HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 20f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;

        TextMeshProUGUI name = NewText("Name", row, label, UITheme.TextStyle.Body, 26f, TextMain, TextAlignmentOptions.MidlineLeft);
        Size(name.gameObject, 230f, -1f);

        // --- Slider ---
        RectTransform sliderRt = NewRect("Slider", row);
        Size(sliderRt.gameObject, -1f, -1f, 1f);

        Image bg = NewImage("Background", sliderRt, StoneDark);
        bg.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        bg.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        bg.rectTransform.sizeDelta = new Vector2(0f, 12f);
        AddBorder(bg.gameObject, Bronze);

        RectTransform fillArea = NewRect("Fill Area", sliderRt);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(-14f, 12f);

        Image fill = NewImage("Fill", fillArea, Bronze);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.sizeDelta = new Vector2(10f, 0f);

        RectTransform handleArea = NewRect("Handle Slide Area", sliderRt);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(10f, 0f);
        handleArea.offsetMax = new Vector2(-10f, 0f);

        Image handle = NewImage("Handle", handleArea, Gold);
        handle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(20f, 34f);

        Slider slider = sliderRt.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        TextMeshProUGUI percent = NewText("Percent", row, "", UITheme.TextStyle.Body, 26f, Gold, TextAlignmentOptions.MidlineRight);
        Size(percent.gameObject, 90f, -1f);

        slider.SetValueWithoutNotify(initial);
        percent.text = Mathf.RoundToInt(initial * 100f) + "%";

        slider.onValueChanged.AddListener(value =>
        {
            percent.text = Mathf.RoundToInt(value * 100f) + "%";
            onChanged?.Invoke(value);
        });

        return slider;
    }

    /// <summary>Actualiza un slider (y su porcentaje) sin disparar el evento.</summary>
    public static void SetSliderSilently(Slider slider, float value)
    {
        if (slider == null) return;
        slider.SetValueWithoutNotify(value);

        TextMeshProUGUI percent = slider.transform.parent.Find("Percent")?.GetComponent<TextMeshProUGUI>();
        if (percent != null) percent.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
