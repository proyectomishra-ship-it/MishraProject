using System;
using UnityEngine;
using TMPro;

/// <summary>
/// Fuente unica de verdad del estilo visual del UI de Mishra:
/// paleta, tipografias, estilos de texto y sprites del kit base.
/// Se crea desde Assets > Create > Mishra > UI Theme y se guarda en
/// Assets/Resources/UITheme.asset para poder accederse con UITheme.Current.
/// </summary>
[CreateAssetMenu(fileName = "UITheme", menuName = "Mishra/UI Theme")]
public class UITheme : ScriptableObject
{
    // =========================
    // ACCESO GLOBAL
    // =========================

    private const string ResourcePath = "UITheme";
    private static UITheme current;

    /// <summary>Tema activo. Se carga una vez desde Resources/UITheme.</summary>
    public static UITheme Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<UITheme>(ResourcePath);
                if (current == null)
                    Debug.LogWarning("[UITheme] No se encontro Resources/UITheme.asset. " +
                                     "Usa Mishra > UI > Create Default Theme.");
            }
            return current;
        }
    }

    // Si se edita el asset en Play Mode, los componentes Themed* se refrescan.
    public static event Action OnThemeChanged;

    private void OnValidate() => OnThemeChanged?.Invoke();

    // =========================
    // PALETA
    // =========================

    [Header("Piedra y metal")]
    public Color stoneDark   = Hex("1B1816");
    public Color stoneMid    = Hex("2E2924");
    public Color bronze      = Hex("A8783A");
    public Color gold        = Hex("D9B165");

    [Header("Texto")]
    public Color textPrimary = Hex("E8DCC0");   // pergamino
    public Color textMuted   = Hex("9C927C");
    public Color textAccent  = Hex("D9B165");   // dorado
    public Color textDanger  = Hex("C0392B");

    [Header("Bordes (UI de color plano)")]
    [Tooltip("Grosor en pixeles del borde que se dibuja con el componente Outline.")]
    public float borderThickness = 2f;

    [Header("Acento y estados")]
    public Color accent  = Hex("C8642B");       // ascua
    public Color success = Hex("5E9E4A");
    public Color danger  = Hex("C0392B");

    [Header("Recursos (barras)")]
    public Color health  = Hex("B3262E");
    public Color mana    = Hex("2F5FB5");
    public Color xp      = Hex("D4A62A");
    public Color stamina = Hex("4F8F3A");

    [Header("Rareza de items")]
    public Color rarityCommon    = Hex("C9C2B2");
    public Color rarityUncommon  = Hex("4FA843");
    public Color rarityRare      = Hex("3D7FD6");
    public Color rarityEpic      = Hex("A046D6");
    public Color rarityLegendary = Hex("E8872A");

    // =========================
    // TIPOGRAFIA
    // =========================

    [Serializable]
    public class TextStyleData
    {
        public TMP_FontAsset font;
        public float fontSize = 24f;
        public Color color = Color.white;
        public float characterSpacing = 0f;
        public bool allCaps = false;
    }

    public enum TextStyle { Title, Heading, Body, Caption, Button }

    [Header("Estilos de texto (si 'font' esta vacio se deja la fuente actual)")]
    public TextStyleData title   = new TextStyleData { fontSize = 64f, characterSpacing = 6f, allCaps = true };
    public TextStyleData heading = new TextStyleData { fontSize = 36f, characterSpacing = 3f };
    public TextStyleData body    = new TextStyleData { fontSize = 24f };
    public TextStyleData caption = new TextStyleData { fontSize = 18f };
    public TextStyleData button  = new TextStyleData { fontSize = 28f, characterSpacing = 4f, allCaps = true };

    public TextStyleData GetTextStyle(TextStyle style)
    {
        switch (style)
        {
            case TextStyle.Title:   return title;
            case TextStyle.Heading: return heading;
            case TextStyle.Caption: return caption;
            case TextStyle.Button:  return button;
            default:                return body;
        }
    }

    // =========================
    // SPRITES DEL KIT BASE
    // =========================

    public enum SpriteRole
    {
        Panel,
        Button, ButtonHover, ButtonPressed,
        Slot,
        InputField,
        BarFrame,
        BarFillHealth, BarFillMana, BarFillXp,
        LevelMedallion,
        Divider
    }

    [Serializable]
    public class ThemeSprites
    {
        [Tooltip("9-slice")] public Sprite panel;
        [Tooltip("9-slice")] public Sprite button;
        [Tooltip("9-slice")] public Sprite buttonHover;
        [Tooltip("9-slice")] public Sprite buttonPressed;
        [Tooltip("9-slice")] public Sprite slot;
        [Tooltip("9-slice")] public Sprite inputField;
        [Tooltip("9-slice")] public Sprite barFrame;
        public Sprite barFillHealth;
        public Sprite barFillMana;
        public Sprite barFillXp;
        public Sprite levelMedallion;
        public Sprite divider;
    }

    [Header("Sprites del kit (OPCIONALES: si estan vacios se usan colores planos)")]
    public ThemeSprites sprites = new ThemeSprites();

    public Sprite GetSprite(SpriteRole role)
    {
        switch (role)
        {
            case SpriteRole.Panel:          return sprites.panel;
            case SpriteRole.Button:         return sprites.button;
            case SpriteRole.ButtonHover:    return sprites.buttonHover;
            case SpriteRole.ButtonPressed:  return sprites.buttonPressed;
            case SpriteRole.Slot:           return sprites.slot;
            case SpriteRole.InputField:     return sprites.inputField;
            case SpriteRole.BarFrame:       return sprites.barFrame;
            case SpriteRole.BarFillHealth:  return sprites.barFillHealth;
            case SpriteRole.BarFillMana:    return sprites.barFillMana;
            case SpriteRole.BarFillXp:      return sprites.barFillXp;
            case SpriteRole.LevelMedallion: return sprites.levelMedallion;
            case SpriteRole.Divider:        return sprites.divider;
            default:                        return null;
        }
    }

    // =========================
    // COLOR PLANO (sin sprites propios)
    // =========================

    /// <summary>
    /// Color que se usa para cada elemento cuando NO hay sprite en el kit.
    /// Permite un UI coherente solo con colores y los sprites que trae Unity.
    /// </summary>
    public Color GetFlatColor(SpriteRole role)
    {
        switch (role)
        {
            case SpriteRole.Panel:          return WithAlpha(stoneDark, 0.9f);
            case SpriteRole.Button:         return stoneMid;
            case SpriteRole.ButtonHover:    return bronze;
            case SpriteRole.ButtonPressed:  return stoneDark;
            case SpriteRole.Slot:           return WithAlpha(stoneDark, 0.75f);
            case SpriteRole.InputField:     return stoneDark;
            case SpriteRole.BarFrame:       return stoneDark;
            case SpriteRole.BarFillHealth:  return health;
            case SpriteRole.BarFillMana:    return mana;
            case SpriteRole.BarFillXp:      return xp;
            case SpriteRole.LevelMedallion: return stoneMid;
            default:                        return gold;
        }
    }

    private static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    // =========================
    // HELPERS
    // =========================

    public Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Uncommon:  return rarityUncommon;
            case ItemRarity.Rare:      return rarityRare;
            case ItemRarity.Epic:      return rarityEpic;
            case ItemRarity.Legendary: return rarityLegendary;
            default:                   return rarityCommon;
        }
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }
}
