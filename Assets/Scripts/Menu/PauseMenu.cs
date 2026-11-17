using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menú de pausa en partida (Esc): Continuar, Configuraciones, Volver al menú principal.
/// Se crea solo al iniciar el juego (no hay que ponerlo en ninguna escena) y solo actúa
/// cuando existe un jugador local, o sea dentro de la partida.
///  - No se abre si ya hay otra ventana tomando el control (inventario, misiones, mejoras de habilidad, muerte).
///  - Si sos el único jugador (host solo) congela el tiempo; con más gente la partida sigue.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    private const string FallbackMenuScene = "MainMenuScene";
    private const float ConfirmSeconds = 4f;

    private static PauseMenu instance;

    public static bool IsOpen => instance != null && instance.open;

    private GameObject root;
    private Button leaveButton;

    private bool open;
    private bool timePaused;
    private float previousTimeScale = 1f;
    private bool confirmLeave;
    private float confirmTimer;

    // Estado "había algo abierto el frame anterior": evita que el mismo Esc que cierra
    // otra ventana abra también la pausa.
    private bool busyLastFrame;

    private Player player;
    private PlayerInputController input;
    private PlayerCameraBinder cameraBinder;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        Canvas canvas = MenuUIKit.CreateCanvas("PauseMenu", 100);
        Object.DontDestroyOnLoad(canvas.gameObject);
        instance = canvas.gameObject.AddComponent<PauseMenu>();
        instance.Build(canvas.transform);
    }

    private void Build(Transform canvas)
    {
        RectTransform panel = MenuUIKit.NewModal(canvas, "PausePanel", 620f, 610f, out root);

        var title = MenuUIKit.NewText("Title", panel, "Pausa", UITheme.TextStyle.Heading, 48f,
            MenuUIKit.Gold, TextAlignmentOptions.Center);
        MenuUIKit.Size(title.gameObject, -1f, 66f);
        MenuUIKit.NewDivider(panel);
        MenuUIKit.NewSpacer(panel, 10f);

        MenuUIKit.NewButton(panel, "Continuar", 66f, Resume);
        MenuUIKit.NewButton(panel, "Configuraciones", 66f, () => SettingsPanel.Show());
        MenuUIKit.NewButton(panel, "Controles", 66f, () => ControlsPanel.Show());
        leaveButton = MenuUIKit.NewButton(panel, "Volver al menú", 66f, OnLeaveClicked);

        MenuUIKit.NewSpacer(panel, 6f);
        var hint = MenuUIKit.NewText("Hint", panel, "Esc para continuar", UITheme.TextStyle.Caption, 20f,
            MenuUIKit.TextSoft, TextAlignmentOptions.Center);
        MenuUIKit.Size(hint.gameObject, -1f, 28f);

        root.SetActive(false);
    }

    // =========================================================
    // LOOP
    // =========================================================

    private void Update()
    {
        bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        if (open)
        {
            // El jugador desapareció (desconexión, cambio de escena): se suelta todo.
            if (player == null)
            {
                Resume();
                return;
            }

            if (esc && !busyLastFrame)
            {
                Resume();
                return;
            }

            ApplyHold();
            UpdateConfirm();
            return;
        }

        if (!esc || busyLastFrame) return;

        Player local = GetLocalPlayer();
        if (local == null || local.IsDead()) return;

        PlayerInputController pic = local.GetComponent<PlayerInputController>();
        if (pic == null || pic.IsInputBlocked) return;

        Open(local, pic);
    }

    private void LateUpdate()
    {
        // Foto del estado al final del frame (después de que todas las ventanas procesaron su Esc).
        bool questOpen = QuestGiverUI.Instance != null && QuestGiverUI.Instance.IsOpen;

        busyLastFrame =
            SettingsPanel.IsOpen ||
            AboutPanel.IsOpen ||
            ControlsPanel.IsOpen ||
            questOpen ||
            SkillUpgradeOfferUI.IsOpen ||
            (!open && IsLocalInputBlocked());
    }

    private bool IsLocalInputBlocked()
    {
        Player local = GetLocalPlayer();
        if (local == null) return false;

        PlayerInputController pic = local.GetComponent<PlayerInputController>();
        return pic != null && pic.IsInputBlocked;
    }

    private static Player GetLocalPlayer()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || nm.LocalClient == null) return null;

        NetworkObject obj = nm.LocalClient.PlayerObject;
        return obj != null ? obj.GetComponent<Player>() : null;
    }

    // =========================================================
    // ABRIR / CERRAR
    // =========================================================

    private void Open(Player p, PlayerInputController pic)
    {
        player = p;
        input = pic;
        cameraBinder = p.GetComponent<PlayerCameraBinder>();

        MenuUIKit.EnsureEventSystem();

        open = true;
        ResetConfirm();
        root.SetActive(true);
        root.transform.SetAsLastSibling();

        AudioManager.PlayUI(UISound.PanelOpen);
        ApplyHold();
    }

    /// <summary>Mantiene al jugador quieto con el mouse libre (se repite cada frame mientras está abierto).</summary>
    private void ApplyHold()
    {
        if (input != null) input.IsInputBlocked = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (cameraBinder != null) cameraBinder.SetLookEnabled(false);

        SetPaused(ShouldPauseTime());
    }

    private void Resume()
    {
        if (!open) return;

        open = false;
        ResetConfirm();
        SettingsPanel.Hide();
        ControlsPanel.Hide();
        if (root != null) root.SetActive(false);

        SetPaused(false);

        // Si el jugador murió mientras tanto, el control de respawn decide cuándo devolverle el input.
        bool dead = player != null && player.IsDead();
        if (input != null && !dead) input.IsInputBlocked = false;

        if (player != null)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (cameraBinder != null) cameraBinder.SetLookEnabled(true);
        }

        AudioManager.PlayUI(UISound.PanelClose);

        player = null;
        input = null;
        cameraBinder = null;
    }

    // =========================================================
    // TIEMPO
    // =========================================================

    private static bool ShouldPauseTime()
    {
        NetworkManager nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && nm.IsServer && nm.ConnectedClientsIds.Count <= 1;
    }

    private void SetPaused(bool pause)
    {
        if (pause == timePaused) return;

        if (pause)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        }

        timePaused = pause;
    }

    private void OnDisable() => SetPaused(false);

    // =========================================================
    // VOLVER AL MENÚ
    // =========================================================

    private void OnLeaveClicked()
    {
        if (!confirmLeave)
        {
            confirmLeave = true;
            confirmTimer = ConfirmSeconds;
            MenuUIKit.SetButtonLabel(leaveButton, "¿Seguro? Click de nuevo");
            return;
        }

        LeaveToMenu();
    }

    private void UpdateConfirm()
    {
        if (!confirmLeave) return;

        confirmTimer -= Time.unscaledDeltaTime;
        if (confirmTimer <= 0f) ResetConfirm();
    }

    private void ResetConfirm()
    {
        confirmLeave = false;
        MenuUIKit.SetButtonLabel(leaveButton, "Volver al menú");
    }

    private void LeaveToMenu()
    {
        // Se suelta todo ANTES de cambiar de escena (tiempo normal, input, cursor).
        SettingsPanel.Hide();
        ControlsPanel.Hide();
        SetPaused(false);
        Time.timeScale = 1f;

        if (input != null) input.IsInputBlocked = false;

        open = false;
        if (root != null) root.SetActive(false);
        player = null;
        input = null;
        cameraBinder = null;

        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
            nm.Shutdown();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        string scene = FallbackMenuScene;
        if (!SceneInBuild(scene))
            scene = "MainMenu";

        SceneManager.LoadScene(scene);
    }

    private static bool SceneInBuild(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                return true;
        }
        return false;
    }
}
