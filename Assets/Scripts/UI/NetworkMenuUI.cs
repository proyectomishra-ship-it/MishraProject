using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

/// <summary>
/// Menú de conexión principal.
/// 
/// SETUP EN UNITY:
///   1. Crear un Canvas en la escena con este script
///   2. Asignar los campos en el Inspector
///   3. El menú se oculta automáticamente al conectar
/// </summary>
public class NetworkMenuUI : MonoBehaviour
{
    [Header("Panel principal")]
    [SerializeField] private GameObject menuPanel;

    [Header("Campos")]
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TMP_InputField portInputField;
    [SerializeField] private TMP_InputField playerNameInputField;

    [Header("Botones")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button quitButton;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject loadingIndicator;

    // Valores por defecto
    private const string DEFAULT_IP = "127.0.0.1";
    private const ushort DEFAULT_PORT = 7777;

    private void Start()
    {
        // Valores por defecto
        if (ipInputField != null) ipInputField.text = DEFAULT_IP;
        if (portInputField != null) portInputField.text = DEFAULT_PORT.ToString();
        if (playerNameInputField != null) playerNameInputField.text = "Jugador";

        // Botones
        hostButton?.onClick.AddListener(OnHostClicked);
        joinButton?.onClick.AddListener(OnJoinClicked);
        quitButton?.onClick.AddListener(OnQuitClicked);

        // Ocultar loading
        if (loadingIndicator != null) loadingIndicator.SetActive(false);

        SetStatus("");

        // Suscribir eventos de red
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        ShowMenu();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // =========================
    // BOTONES
    // =========================

    private void OnHostClicked()
    {
        if (NetworkManager.Singleton.IsListening) return;

        ushort port = ParsePort();
        ConfigureTransport("0.0.0.0", port); // host escucha en todas las interfaces

        SetStatus("Iniciando servidor...");
        SetLoading(true);
        SetButtonsInteractable(false);

        NetworkManager.Singleton.StartHost();
    }

    private void OnJoinClicked()
    {
        if (NetworkManager.Singleton.IsListening) return;

        string ip = GetIP();
        ushort port = ParsePort();

        if (string.IsNullOrWhiteSpace(ip))
        {
            SetStatus("Ingresa una IP valida");
            return;
        }

        ConfigureTransport(ip, port);

        SetStatus($"Conectando a {ip}:{port}...");
        SetLoading(true);
        SetButtonsInteractable(false);

        NetworkManager.Singleton.StartClient();
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // =========================
    // EVENTOS DE RED
    // =========================

    private void OnServerStarted()
    {
        SetStatus("Servidor iniciado - esperando jugadores...");
        SetLoading(false);
        HideMenu();

        string localIP = GetLocalIP();
        Debug.Log($"[Network] Host iniciado. IP local: {localIP} | Puerto: {ParsePort()}");
        Debug.Log($"[Network] Compartí esta IP con tus compañeros: {localIP}");
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            SetStatus("Conectado!");
            SetLoading(false);
            HideMenu();
            Debug.Log($"[Network] Conectado al servidor. ClientId: {clientId}");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId ||
            !NetworkManager.Singleton.IsServer)
        {
            SetStatus("Desconectado del servidor");
            SetLoading(false);
            SetButtonsInteractable(true);
            ShowMenu();
        }
    }

    // =========================
    // HELPERS
    // =========================

    private void ConfigureTransport(string ip, ushort port)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("[Network] No se encontró UnityTransport en el NetworkManager");
            return;
        }
        transport.SetConnectionData(ip, port);
        Debug.Log($"[Network] Transport configurado → {ip}:{port}");
    }

    private string GetIP()
    {
        string ip = ipInputField != null ? ipInputField.text.Trim() : DEFAULT_IP;
        return string.IsNullOrEmpty(ip) ? DEFAULT_IP : ip;
    }

    private ushort ParsePort()
    {
        if (portInputField != null &&
            ushort.TryParse(portInputField.text, out ushort port))
            return port;
        return DEFAULT_PORT;
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    private void SetLoading(bool active)
    {
        if (loadingIndicator != null) loadingIndicator.SetActive(active);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (hostButton != null) hostButton.interactable = interactable;
        if (joinButton != null) joinButton.interactable = interactable;
    }

    private void ShowMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HideMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
    }

    /// <summary>
    /// Obtiene la IP local de la máquina para mostrársela al host.
    /// </summary>
    private string GetLocalIP()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
        }
        catch { }
        return "desconocida";
    }
}