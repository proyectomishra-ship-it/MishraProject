using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Panel de Configuraciones: volumen general / música / efectos / interfaz y pantalla completa.
/// Se arma solo por código (no hay que ponerlo en ninguna escena):
///     SettingsPanel.Show();   SettingsPanel.Hide();
/// Los volúmenes se guardan solos con AudioManager (PlayerPrefs). Esc cierra el panel.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    private static SettingsPanel instance;

    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    private GameObject root;
    private Slider sGeneral, sMusica, sEfectos, sInterfaz;
    private Button fullscreenButton;

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

        // Por encima de todo el resto del UI (cartel de mejoras = 35, avisos = 40).
        Canvas canvas = MenuUIKit.CreateCanvas("SettingsPanelCanvas", 120);
        instance = canvas.gameObject.AddComponent<SettingsPanel>();
        instance.Build(canvas.transform);
    }

    private void Build(Transform canvas)
    {
        RectTransform panel = MenuUIKit.NewModal(canvas, "SettingsPanel", 940f, 700f, out root);

        var title = MenuUIKit.NewText("Title", panel, "Configuraciones", UITheme.TextStyle.Heading, 44f,
            MenuUIKit.Gold, TMPro.TextAlignmentOptions.Center);
        MenuUIKit.Size(title.gameObject, -1f, 60f);
        MenuUIKit.NewDivider(panel);
        MenuUIKit.NewSpacer(panel, 6f);

        var audioHeader = MenuUIKit.NewText("AudioHeader", panel, "Sonido", UITheme.TextStyle.Body, 24f,
            MenuUIKit.TextSoft, TMPro.TextAlignmentOptions.MidlineLeft);
        MenuUIKit.Size(audioHeader.gameObject, -1f, 32f);

        sGeneral  = MenuUIKit.NewSliderRow(panel, "General",   AudioManager.GetVolume(AudioCategory.General),   v => AudioManager.SetVolume(AudioCategory.General, v));
        sMusica   = MenuUIKit.NewSliderRow(panel, "Música",    AudioManager.GetVolume(AudioCategory.Musica),    v => AudioManager.SetVolume(AudioCategory.Musica, v));
        sEfectos  = MenuUIKit.NewSliderRow(panel, "Efectos",   AudioManager.GetVolume(AudioCategory.Efectos),   v => AudioManager.SetVolume(AudioCategory.Efectos, v));
        sInterfaz = MenuUIKit.NewSliderRow(panel, "Interfaz",  AudioManager.GetVolume(AudioCategory.Interfaz),  v => AudioManager.SetVolume(AudioCategory.Interfaz, v));

        MenuUIKit.NewSpacer(panel, 6f);
        var videoHeader = MenuUIKit.NewText("VideoHeader", panel, "Pantalla", UITheme.TextStyle.Body, 24f,
            MenuUIKit.TextSoft, TMPro.TextAlignmentOptions.MidlineLeft);
        MenuUIKit.Size(videoHeader.gameObject, -1f, 32f);

        fullscreenButton = MenuUIKit.NewButton(panel, "", 56f, ToggleFullscreen, 24f);
        RefreshFullscreenLabel();

        MenuUIKit.NewSpacer(panel, 10f);

        var buttons = MenuUIKit.NewRect("Buttons", panel);
        MenuUIKit.Size(buttons.gameObject, -1f, 60f, 1f);
        var h = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 20f;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;

        MenuUIKit.NewButton(buttons, "Controles", 60f, ControlsPanel.Show, 24f);
        MenuUIKit.NewButton(buttons, "Restablecer", 60f, ResetDefaults, 24f);
        MenuUIKit.NewButton(buttons, "Volver", 60f, Close, 24f);

        root.SetActive(false);
    }

    private void Open()
    {
        RefreshAll();
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

    private void RefreshAll()
    {
        MenuUIKit.SetSliderSilently(sGeneral,  AudioManager.GetVolume(AudioCategory.General));
        MenuUIKit.SetSliderSilently(sMusica,   AudioManager.GetVolume(AudioCategory.Musica));
        MenuUIKit.SetSliderSilently(sEfectos,  AudioManager.GetVolume(AudioCategory.Efectos));
        MenuUIKit.SetSliderSilently(sInterfaz, AudioManager.GetVolume(AudioCategory.Interfaz));
        RefreshFullscreenLabel();
    }

    private void ResetDefaults()
    {
        AudioManager.SetVolume(AudioCategory.General, 1f);
        AudioManager.SetVolume(AudioCategory.Musica, 0.7f);
        AudioManager.SetVolume(AudioCategory.Efectos, 1f);
        AudioManager.SetVolume(AudioCategory.Interfaz, 1f);
        RefreshAll();
    }

    private static bool IsFullscreen => Screen.fullScreenMode != FullScreenMode.Windowed;

    private void ToggleFullscreen()
    {
        Resolution desktop = Screen.currentResolution;

        if (IsFullscreen)
        {
            // A ventana: tamaño cómodo (80% del monitor, máximo 1280x720) y redimensionable con el mouse.
            int w = Mathf.Min(1280, Mathf.RoundToInt(desktop.width * 0.8f));
            int h = Mathf.Min(720, Mathf.RoundToInt(desktop.height * 0.8f));
            Screen.SetResolution(w, h, FullScreenMode.Windowed);
        }
        else
        {
            // Pantalla completa sin bordes: cambia rápido y permite Alt+Tab sin problemas.
            Screen.SetResolution(desktop.width, desktop.height, FullScreenMode.FullScreenWindow);
        }

        // El cambio se aplica al final del frame: se muestra el estado al que se va.
        MenuUIKit.SetButtonLabel(fullscreenButton, "Pantalla completa: " + (IsFullscreen ? "No" : "Sí"));
    }

    private void RefreshFullscreenLabel()
    {
        MenuUIKit.SetButtonLabel(fullscreenButton, "Pantalla completa: " + (IsFullscreen ? "Sí" : "No"));
    }
}
