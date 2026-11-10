using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Marca un Button como "elegido" usando la paleta del UITheme:
/// fondo bronce y, si el boton tiene un Outline, borde dorado mas grueso.
/// Para botones que ya usan ThemedButton (por ejemplo, la eleccion de clase).
/// No requiere agregar componentes ni conectar nada en el Inspector.
/// </summary>
public static class ThemedSelection
{
    public static void Set(Button button, bool selected)
    {
        if (button == null) return;

        var theme = UITheme.Current;
        if (theme == null) return;

        // Fondo: piedra normal o mezcla con bronce cuando esta elegido.
        Color normal = selected
            ? Color.Lerp(theme.stoneMid, theme.bronze, 0.55f)
            : theme.stoneMid;

        var colors = button.colors;
        colors.normalColor = normal;
        colors.selectedColor = normal;   // que el resaltado no dependa del foco del EventSystem
        button.colors = colors;

        // Borde (opcional): dorado y doble de grueso cuando esta elegido.
        var outline = button.GetComponent<Outline>();
        if (outline != null)
        {
            float d = theme.borderThickness * (selected ? 2f : 1f);
            outline.effectColor = selected ? theme.gold : theme.bronze;
            outline.effectDistance = new Vector2(d, -d);
        }
    }
}
