using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Estiliza una Image con el UITheme.
///  - Si el kit tiene sprite para el rol, lo usa.
///  - Si no (lo normal sin arte propio), pinta la Image con un color plano de la
///    paleta y conserva el sprite que ya tenga (por ejemplo UISprite, el
///    rectangulo redondeado que trae Unity).
///  - Si la Image tiene un componente Outline, lo configura como borde.
/// </summary>
[RequireComponent(typeof(Image))]
[ExecuteAlways]
public class ThemedImage : MonoBehaviour
{
    [SerializeField] private UITheme.SpriteRole role = UITheme.SpriteRole.Panel;

    [Tooltip("Image.Type.Sliced: mantiene bordes redondeados al estirar (UISprite, paneles, botones).")]
    [SerializeField] private bool useSliced = true;

    private Image image;

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

        if (image == null) image = GetComponent<Image>();
        if (image == null) return;

        var sprite = theme.GetSprite(role);

        if (sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
        }
        else
        {
            image.color = theme.GetFlatColor(role);
        }

        if (useSliced && image.sprite != null)
            image.type = Image.Type.Sliced;

        ConfigureBorder(gameObject, theme);
    }

    /// <summary>Si el objeto tiene un Outline, lo deja con el color y grosor del tema.</summary>
    public static void ConfigureBorder(GameObject go, UITheme theme)
    {
        var outline = go.GetComponent<Outline>();
        if (outline == null) return;

        outline.effectColor = theme.bronze;
        outline.effectDistance = new Vector2(theme.borderThickness, -theme.borderThickness);
        outline.useGraphicAlpha = false;
    }

    /// <summary>Agrega un borde (Outline) desde el menu contextual del componente.</summary>
    [ContextMenu("Agregar borde")]
    private void AddBorder()
    {
        if (GetComponent<Outline>() == null)
            gameObject.AddComponent<Outline>();

        Apply();
    }
}
