using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Da estilo a un Button usando solo la paleta del UITheme (Color Tint):
/// normal = piedra, hover = bronce, presionado = piedra oscura.
/// Pensado para botones SIN arte propio (Image con UISprite o similar).
/// Si el boton usa un sprite propio, no agregues este componente: el tinte
/// cambiaria los colores del dibujo.
/// </summary>
[RequireComponent(typeof(Button))]
[ExecuteAlways]
public class ThemedButton : MonoBehaviour
{
    private Button button;

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

        if (button == null) button = GetComponent<Button>();
        if (button == null) return;

        button.transition = Selectable.Transition.ColorTint;

        var colors = button.colors;
        colors.normalColor      = theme.stoneMid;
        colors.highlightedColor = theme.bronze;
        colors.pressedColor     = theme.stoneDark;
        colors.selectedColor    = theme.stoneMid;   // evita quedarse "iluminado" tras el click
        colors.disabledColor    = new Color(theme.textMuted.r, theme.textMuted.g, theme.textMuted.b, 0.5f);
        colors.colorMultiplier  = 1f;
        colors.fadeDuration     = 0.1f;
        button.colors = colors;

        // El Color Tint se multiplica por el color de la Image: debe ser blanco.
        if (button.targetGraphic is Image img)
            img.color = Color.white;

        ThemedImage.ConfigureBorder(gameObject, theme);
    }
}
