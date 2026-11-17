using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Panel "Controles": lista de teclas del juego. Se arma por código: ControlsPanel.Show() / Hide().
/// Para cambiar o agregar controles editá la lista de abajo.
/// </summary>
public class ControlsPanel : MonoBehaviour
{
    // =========================================================
    // CONTROLES (editar acá): { tecla, acción }
    // =========================================================
    private static readonly string[,] Controls =
    {
        { "W A S D / Flechas",  "Moverse" },
        { "Mouse",              "Mover la cámara" },
        { "Shift",              "Correr" },
        { "Espacio",            "Saltar" },
        { "Click izquierdo",    "Ataque" },
        { "Click derecho",      "Ataque especial" },
        { "E",                  "Interactuar (cofres, NPCs)" },
        { "Tab",                "Inventario, crafteo y estadísticas" },
        { "Esc",                "Pausa / cerrar ventanas" },
    };

    // =========================================================

    private static ControlsPanel instance;
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
        Canvas canvas = MenuUIKit.CreateCanvas("ControlsPanelCanvas", 125);
        instance = canvas.gameObject.AddComponent<ControlsPanel>();
        instance.Build(canvas.transform);
    }

    private void Build(Transform canvas)
    {
        RectTransform panel = MenuUIKit.NewModal(canvas, "ControlsPanel", 900f, 760f, out root);

        var title = MenuUIKit.NewText("Title", panel, "Controles", UITheme.TextStyle.Heading, 44f,
            MenuUIKit.Gold, TextAlignmentOptions.Center);
        MenuUIKit.Size(title.gameObject, -1f, 60f);
        MenuUIKit.NewDivider(panel);
        MenuUIKit.NewSpacer(panel, 6f);

        for (int i = 0; i < Controls.GetLength(0); i++)
        {
            RectTransform row = MenuUIKit.NewRect("Row_" + i, panel);
            MenuUIKit.Size(row.gameObject, -1f, 44f, 1f);

            var h = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            h.spacing = 24f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;

            var key = MenuUIKit.NewText("Key", row, Controls[i, 0], UITheme.TextStyle.Body, 26f,
                MenuUIKit.Gold, TextAlignmentOptions.MidlineLeft);
            MenuUIKit.Size(key.gameObject, 330f, -1f);

            var action = MenuUIKit.NewText("Action", row, Controls[i, 1], UITheme.TextStyle.Body, 26f,
                MenuUIKit.TextMain, TextAlignmentOptions.MidlineLeft);
            MenuUIKit.Size(action.gameObject, -1f, -1f, 1f);
        }

        MenuUIKit.NewSpacer(panel, 8f);
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
