using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pantalla de victoria al derrotar al jefe: aviso tipo logro con dos opciones,
///   - "Seguir explorando": cierra la pantalla y el jugador sigue jugando (farmear, misiones, etc.)
///   - "Reiniciar aventura": sale de la partida y vuelve al menu principal para empezar de nuevo.
///
/// Se crea sola cuando hace falta (como PauseMenu y SkillNoticeUI): no hay que tocar la escena
/// ni crear prefabs. Usa solo la UI de Unity (Image + TextMeshPro) y los colores del UITheme
/// a traves de MenuUIKit, igual que el resto de los menus.
///
/// Uso:  BossVictoryUI.Show("al Semidiós");   // se lee "Derrotaste al Semidiós."
/// </summary>
public class BossVictoryUI : MonoBehaviour
{
    private const string FallbackMenuScene = "MainMenuScene";
    private const int SortingOrder = 90;          // por debajo del menu de pausa (100)
    private const float ShowDelay = 2.5f;         // deja ver la muerte del jefe antes del aviso

    private static BossVictoryUI instance;

    private GameObject root;
    private TextMeshProUGUI subtitle;
    private Player player;
    private PlayerInputController input;
    private PlayerCameraBinder cameraBinder;
    private Coroutine pending;
    private bool open;

    // =========================================================
    // API
    // =========================================================

    /// <summary>Muestra la pantalla de victoria en este cliente (despues de una pequena pausa).</summary>
    public static void Show(string bossName)
    {
        if (instance == null)
        {
            Canvas canvas = MenuUIKit.CreateCanvas("BossVictoryUI", SortingOrder);
            Object.DontDestroyOnLoad(canvas.gameObject);
            instance = canvas.gameObject.AddComponent<BossVictoryUI>();
            instance.Build(canvas.transform);
        }

        if (instance.open || instance.pending != null)
            return;                                // ya se esta mostrando

        instance.pending = instance.StartCoroutine(instance.ShowAfterDelay(bossName));
    }

    // =========================================================
    // CONSTRUCCION
    // =========================================================

    private void Build(Transform canvas)
    {
        RectTransform panel = MenuUIKit.NewModal(canvas, "VictoryPanel", 900f, 640f, out root);

        var banner = MenuUIKit.NewText("Banner", panel, "LOGRO DESBLOQUEADO", UITheme.TextStyle.Caption, 22f,
            MenuUIKit.TextSoft, TextAlignmentOptions.Center);
        MenuUIKit.Size(banner.gameObject, -1f, 36f);

        var title = MenuUIKit.NewText("Title", panel, "¡Venciste al jefe!", UITheme.TextStyle.Title, 60f,
            MenuUIKit.Gold, TextAlignmentOptions.Center);
        // Una sola linea: si el texto no entra, se achica solo en vez de partirse en dos.
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.enableAutoSizing = true;
        title.fontSizeMax = 60f;
        title.fontSizeMin = 30f;
        title.overflowMode = TextOverflowModes.Overflow;
        MenuUIKit.Size(title.gameObject, -1f, 110f);

        MenuUIKit.NewDivider(panel);

        subtitle = MenuUIKit.NewText("Subtitle", panel, "", UITheme.TextStyle.Body, 28f,
            MenuUIKit.TextMain, TextAlignmentOptions.Center);
        MenuUIKit.Size(subtitle.gameObject, -1f, 70f);

        MenuUIKit.NewSpacer(panel, 14f);

        MenuUIKit.NewButton(panel, "Seguir explorando", 66f, Continue);
        MenuUIKit.NewButton(panel, "Reiniciar aventura", 66f, RestartAdventure);

        root.SetActive(false);
    }

    // =========================================================
    // ABRIR / CERRAR
    // =========================================================

    private IEnumerator ShowAfterDelay(string bossName)
    {
        yield return new WaitForSecondsRealtime(ShowDelay);
        pending = null;

        player = GetLocalPlayer();
        if (player == null)
            yield break;                           // el jugador ya no esta (desconexion / cambio de escena)

        input = player.GetComponent<PlayerInputController>();
        cameraBinder = player.GetComponent<PlayerCameraBinder>();

        subtitle.text = string.IsNullOrEmpty(bossName)
            ? "Derrotaste al jefe de la zona."
            : "Derrotaste " + bossName + ".";

        MenuUIKit.EnsureEventSystem();

        open = true;
        root.SetActive(true);
        root.transform.SetAsLastSibling();

        AudioManager.PlayUI(UISound.QuestComplete);
        ApplyHold();
    }

    private void Update()
    {
        if (!open) return;

        // El jugador desaparecio (desconexion): se cierra solo.
        if (player == null)
        {
            Close(false);
            return;
        }

        ApplyHold();
    }

    /// <summary>Mientras esta abierta: jugador quieto y mouse libre (el juego NO se pausa: puede haber mas gente).</summary>
    private void ApplyHold()
    {
        if (input != null) input.IsInputBlocked = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (cameraBinder != null) cameraBinder.SetLookEnabled(false);
    }

    private void Continue()
    {
        Close(true);
    }

    private void Close(bool restoreControl)
    {
        open = false;
        if (root != null) root.SetActive(false);

        if (restoreControl && player != null)
        {
            // Si el jugador murio mientras tanto, el control de respawn decide cuando devolverle el input.
            if (input != null && !player.IsDead()) input.IsInputBlocked = false;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (cameraBinder != null) cameraBinder.SetLookEnabled(true);
        }

        player = null;
        input = null;
        cameraBinder = null;
    }

    // =========================================================
    // REINICIAR
    // =========================================================

    private void RestartAdventure()
    {
        if (input != null) input.IsInputBlocked = false;
        Close(false);

        Time.timeScale = 1f;

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

    private static Player GetLocalPlayer()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || nm.LocalClient == null) return null;

        NetworkObject obj = nm.LocalClient.PlayerObject;
        return obj != null ? obj.GetComponent<Player>() : null;
    }
}