using UnityEngine;
using TMPro;

/// <summary>
/// Aplica un estilo del UITheme (fuente, tamano, color, espaciado) a un TMP_Text.
/// Agregalo a cualquier texto existente y elige el estilo: no hace falta tocar
/// los scripts que escriben en .text.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
[ExecuteAlways]
public class ThemedText : MonoBehaviour
{
    [SerializeField] private UITheme.TextStyle style = UITheme.TextStyle.Body;

    [Tooltip("Mantener el tamano de fuente que ya tiene el texto en la escena.")]
    [SerializeField] private bool keepFontSize = false;

    [Tooltip("Mantener el color que ya tiene el texto (por ejemplo, texto de rareza).")]
    [SerializeField] private bool keepColor = false;

    private TMP_Text label;

    private void OnEnable()
    {
        UITheme.OnThemeChanged += Apply;
        Apply();
    }

    private void OnDisable() => UITheme.OnThemeChanged -= Apply;

    private void OnValidate() => Apply();

    public void Apply()
    {
        var theme = UITheme.Current;
        if (theme == null) return;

        if (label == null) label = GetComponent<TMP_Text>();
        if (label == null) return;

        var s = theme.GetTextStyle(style);

        if (s.font != null) label.font = s.font;
        if (!keepFontSize)  label.fontSize = s.fontSize;
        if (!keepColor)     label.color = s.color.a > 0f && s.color != Color.white
                                ? s.color
                                : DefaultColorFor(theme, style);

        label.characterSpacing = s.characterSpacing;

        if (s.allCaps) label.fontStyle |= FontStyles.UpperCase;
        else           label.fontStyle &= ~FontStyles.UpperCase;
    }

    // Si el estilo no define un color propio, se usa el de la paleta.
    private static Color DefaultColorFor(UITheme theme, UITheme.TextStyle style)
    {
        switch (style)
        {
            case UITheme.TextStyle.Title:
            case UITheme.TextStyle.Heading:
            case UITheme.TextStyle.Button:  return theme.textAccent;
            case UITheme.TextStyle.Caption: return theme.textMuted;
            default:                        return theme.textPrimary;
        }
    }
}
