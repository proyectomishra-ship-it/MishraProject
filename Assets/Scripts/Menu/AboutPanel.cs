using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Panel "Sobre nosotros". Se arma por código: AboutPanel.Show() / AboutPanel.Hide().
/// Para cambiar los textos editá la sección TEXTOS de abajo (no hay nada más que tocar).
/// </summary>
public class AboutPanel : MonoBehaviour
{
    // =========================================================
    // TEXTOS (editar acá)
    // =========================================================
    private const string GameTitle = "MISHRA";
    private const string Subtitle  = "RPG 3D multijugador de mundo abierto";

    // Un integrante por línea. COMPLETAR con los nombres (y roles) del equipo.
    private static readonly string[] Team =
    {
        "Álvaro-Programador/Productor",
        "Adrian Laborde-Programador/Desing",
        "Abigail Juncos-Artista",
        "Alexis Emiliano Barrera Torrico-Artista/Sonidos",
        "Paula Sabrina Melones-Artista/Desing",
        // "Nombre del compañero — Rol",
    };

    private const string Thanks = "Gracias por jugar.";
   

    // =========================================================

    private static AboutPanel instance;
    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    private GameObject root;

    public static void Show()
    {
        if (instance == null) Create();
        instance.Open();
    }

    public static void Hide()
    {
        if (instance != null) instance.Close();
    }

    private static void Create()
    {
        MenuUIKit.EnsureEventSystem();
        Canvas canvas = MenuUIKit.CreateCanvas("AboutPanelCanvas", 120);
        instance = canvas.gameObject.AddComponent<AboutPanel>();
        instance.Build(canvas.transform);
    }

    private void Build(Transform canvas)
    {
        RectTransform panel = MenuUIKit.NewModal(canvas, "AboutPanel", 860f, 760f, out root);

        var title = MenuUIKit.NewText("Title", panel, "Sobre nosotros", UITheme.TextStyle.Heading, 44f,
            MenuUIKit.Gold, TextAlignmentOptions.Center);
        MenuUIKit.Size(title.gameObject, -1f, 60f);
        MenuUIKit.NewDivider(panel);
        MenuUIKit.NewSpacer(panel, 8f);

        var game = MenuUIKit.NewText("Game", panel, GameTitle, UITheme.TextStyle.Title, 56f,
            MenuUIKit.TextMain, TextAlignmentOptions.Center);
        MenuUIKit.Size(game.gameObject, -1f, 76f);

        var sub = MenuUIKit.NewText("Subtitle", panel, Subtitle, UITheme.TextStyle.Body, 24f,
            MenuUIKit.TextSoft, TextAlignmentOptions.Center);
        MenuUIKit.Size(sub.gameObject, -1f, 34f);

        MenuUIKit.NewSpacer(panel, 10f);

        var teamHeader = MenuUIKit.NewText("TeamHeader", panel, "Equipo de desarrollo", UITheme.TextStyle.Body, 26f,
            MenuUIKit.Gold, TextAlignmentOptions.Center);
        MenuUIKit.Size(teamHeader.gameObject, -1f, 36f);

        var team = MenuUIKit.NewText("Team", panel, string.Join("\n", Team), UITheme.TextStyle.Body, 26f,
            MenuUIKit.TextMain, TextAlignmentOptions.Top);
        MenuUIKit.Size(team.gameObject, -1f, 40f + 32f * Mathf.Max(0, Team.Length - 1));

        MenuUIKit.NewSpacer(panel, 10f);
        MenuUIKit.NewDivider(panel);
        MenuUIKit.NewSpacer(panel, 6f);

        var techHeader = MenuUIKit.NewText("TechHeader", panel, "Tecnología", UITheme.TextStyle.Body, 26f,
            MenuUIKit.Gold, TextAlignmentOptions.Center);
        MenuUIKit.Size(techHeader.gameObject, -1f, 36f);

        var tech = MenuUIKit.NewText("Tech", panel,
            "Unity " + Application.unityVersion + "\nNetcode for GameObjects · Cinemachine · URP",
            UITheme.TextStyle.Body, 22f, MenuUIKit.TextSoft, TextAlignmentOptions.Top);
        MenuUIKit.Size(tech.gameObject, -1f, 70f);

        var version = MenuUIKit.NewText("Version", panel, "Versión " + Application.version, UITheme.TextStyle.Body, 22f,
            MenuUIKit.TextSoft, TextAlignmentOptions.Center);
        MenuUIKit.Size(version.gameObject, -1f, 30f);

        var thanks = MenuUIKit.NewText("Thanks", panel, Thanks, UITheme.TextStyle.Body, 24f,
            MenuUIKit.TextMain, TextAlignmentOptions.Center);
        MenuUIKit.Size(thanks.gameObject, -1f, 40f);

        MenuUIKit.NewSpacer(panel, 6f);
        MenuUIKit.NewButton(panel, "Volver", 60f, Close, 26f);

        root.SetActive(false);
    }

    private void Open()
    {
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        AudioManager.PlayUI(UISound.PanelOpen);
    }

    private void Close()
    {
        if (root == null || !root.activeSelf) return;
        root.SetActive(false);
        AudioManager.PlayUI(UISound.PanelClose);
    }

    private void Update()
    {
        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }
}
