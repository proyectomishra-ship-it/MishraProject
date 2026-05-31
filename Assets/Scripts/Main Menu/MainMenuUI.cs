using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Pantalla principal del juego.
/// 
/// SETUP EN UNITY:
///   1. Crear escena "MainMenu" y agregarla al Build Settings
///   2. Crear un GameObject vacío llamado "MainMenu" y agregarle este script
///   3. Crear el Canvas con los botones y asignarlos en el Inspector
///   4. Asegurarse que "NetworkMenu" y "CharacterSelect" estén en Build Settings
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Botones")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button aboutButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Texto 'Próximamente' (opcional)")]
    [SerializeField] private GameObject comingSoonPanel;
    [SerializeField] private TextMeshProUGUI comingSoonText;

    [Header("Escenas")]
    [SerializeField] private string networkMenuScene = "NetworkMenu";

    private void Start()
    {
        // Botones activos
        startButton?.onClick.AddListener(OnStartClicked);
        quitButton?.onClick.AddListener(OnQuitClicked);

        // Botones futuros — muestran "Próximamente"
        loadButton?.onClick.AddListener(() => ShowComingSoon("Cargar Partida"));
        aboutButton?.onClick.AddListener(() => ShowComingSoon("Sobre Nosotros"));
        settingsButton?.onClick.AddListener(() => ShowComingSoon("Configuraciones"));

        // Visualmente distintos para indicar que no están disponibles
        SetButtonAvailable(loadButton, false);
        SetButtonAvailable(aboutButton, false);
        SetButtonAvailable(settingsButton, false);

        if (comingSoonPanel != null)
            comingSoonPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void OnStartClicked()
    {
        Debug.Log("[MainMenu] Iniciando partida → NetworkMenu");
        SceneManager.LoadScene(networkMenuScene);
    }

    private void ShowComingSoon(string featureName)
    {
        if (comingSoonPanel == null) return;

        comingSoonPanel.SetActive(true);
        if (comingSoonText != null)
            comingSoonText.text = $"{featureName}\nPróximamente";

        // Auto-ocultar después de 2 segundos
        CancelInvoke(nameof(HideComingSoon));
        Invoke(nameof(HideComingSoon), 2f);
    }

    private void HideComingSoon()
    {
        if (comingSoonPanel != null)
            comingSoonPanel.SetActive(false);
    }

    private void SetButtonAvailable(Button btn, bool available)
    {
        if (btn == null) return;

        // Mantener interactable para mostrar el mensaje,
        // pero cambiar el color para indicar que no está disponible
        var colors = btn.colors;
        colors.normalColor   = available ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        btn.colors = colors;
    }

    private void OnQuitClicked()
    {
        Debug.Log("[MainMenu] Saliendo...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
