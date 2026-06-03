using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;

public class NetworkMenuUI : MonoBehaviour
{
    [Header("Panel principal")]
    [SerializeField] private GameObject menuPanel;

    [Header("Tabs")]
    [SerializeField] private GameObject panelLAN;
    [SerializeField] private GameObject panelOnline;
    [SerializeField] private Button tabLANButton;
    [SerializeField] private Button tabOnlineButton;

    [Header("LAN")]
    [SerializeField] private TMP_InputField lanIPInput;
    [SerializeField] private TMP_InputField lanPortInput;
    [SerializeField] private Button lanHostButton;
    [SerializeField] private Button lanJoinButton;

    [Header("Online (Relay)")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TextMeshProUGUI joinCodeDisplay;
    [SerializeField] private Button onlineHostButton;
    [SerializeField] private Button onlineJoinButton;

    [Header("Comun")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button quitButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject loadingIndicator;

    [Header("HUD - Codigo de sala (visible durante la partida)")]
    [SerializeField] private GameObject roomCodePanel;
    [SerializeField] private TextMeshProUGUI roomCodeText;

    [Header("Config")]
    [SerializeField] private int maxPlayers = 6;
    [SerializeField] private string characterSelectScene = "CharacterSelect";
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private Button backButton;

    private const string DEFAULT_IP = "127.0.0.1";
    private const ushort DEFAULT_PORT = 7777;

    private bool servicesInitialized = false;

    private async void Start()
    {
        tabLANButton?.onClick.AddListener(() => ShowTab(true));
        tabOnlineButton?.onClick.AddListener(() => ShowTab(false));

        lanHostButton?.onClick.AddListener(OnLANHostClicked);
        lanJoinButton?.onClick.AddListener(OnLANJoinClicked);

        onlineHostButton?.onClick.AddListener(OnRelayHostClicked);
        onlineJoinButton?.onClick.AddListener(OnRelayJoinClicked);

        quitButton?.onClick.AddListener(OnQuitClicked);
        backButton?.onClick.AddListener(OnBackClicked);

        if (lanIPInput != null) lanIPInput.text = DEFAULT_IP;
        if (lanPortInput != null) lanPortInput.text = DEFAULT_PORT.ToString();
        if (playerNameInput != null) playerNameInput.text = "Jugador";

        SetLoading(false);
        SetStatus("Inicializando servicios...");
        HideRoomCode();
        ShowMenu();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
        else
        {
            Debug.LogError("[NetworkMenuUI] NetworkManager.Singleton es null. Asegurate de que el NetworkManager esté en la escena.");
        }

        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            servicesInitialized = true;
            SetStatus("Listo.");
            Debug.Log($"[Relay] Autenticado. PlayerID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (System.Exception e)
        {
            servicesInitialized = false;
            SetStatus("Modo LAN disponible.");
            Debug.LogWarning($"[Relay] Servicios no disponibles: {e.Message}");
        }
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
    // TABS
    // =========================

    private void ShowTab(bool lan)
    {
        if (panelLAN != null) panelLAN.SetActive(lan);
        if (panelOnline != null) panelOnline.SetActive(!lan);
    }

    // =========================
    // LAN
    // =========================

    private void OnLANHostClicked()
    {
        if (NetworkManager.Singleton.IsListening) return;

        ushort port = ParsePort();
        ConfigureTransportLAN("0.0.0.0", port);

        // Guardar IP y puerto para mostrarlos en el HUD al arrancar
        PlayerPrefs.SetString("LANHostIP", GetLocalIP());
        PlayerPrefs.SetString("LANHostPort", port.ToString());

        SetStatus("Iniciando servidor LAN...");
        SetLoading(true);
        SetButtonsInteractable(false);

        NetworkManager.Singleton.StartHost();
    }

    private void OnLANJoinClicked()
    {
        if (NetworkManager.Singleton.IsListening) return;

        string ip = lanIPInput != null ? lanIPInput.text.Trim() : DEFAULT_IP;
        ushort port = ParsePort();

        if (string.IsNullOrWhiteSpace(ip))
        {
            SetStatus("Ingresa una IP valida");
            return;
        }

        ConfigureTransportLAN(ip, port);
        SetStatus($"Conectando a {ip}:{port}...");
        SetLoading(true);
        SetButtonsInteractable(false);

        NetworkManager.Singleton.StartClient();
    }

    private void ConfigureTransportLAN(string ip, ushort port)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null) return;
        transport.SetConnectionData(ip, port);
        Debug.Log($"[LAN] Transport -> {ip}:{port}");
    }

    // =========================
    // RELAY (ONLINE)
    // =========================

    private async void OnRelayHostClicked()
    {
        if (!servicesInitialized)
        {
            SetStatus("Servicios online no disponibles. Usa LAN.");
            return;
        }

        if (NetworkManager.Singleton.IsListening) return;

        SetStatus("Creando sala online...");
        SetLoading(true);
        SetButtonsInteractable(false);

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Debug.Log($"[Relay] Sala creada. Codigo: {joinCode}");

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));

            // Guardar el codigo para mostrarlo en el HUD
            PlayerPrefs.SetString("RoomCode", joinCode);

            NetworkManager.Singleton.StartHost();
        }
        catch (System.Exception e)
        {
            SetStatus($"Error al crear sala: {e.Message}");
            SetLoading(false);
            SetButtonsInteractable(true);
            Debug.LogError($"[Relay] Error: {e}");
        }
    }

    private async void OnRelayJoinClicked()
    {
        if (!servicesInitialized)
        {
            SetStatus("Servicios online no disponibles. Usa LAN.");
            return;
        }

        if (NetworkManager.Singleton.IsListening) return;

        string code = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpper() : "";

        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("Ingresa el codigo de sala");
            return;
        }

        SetStatus($"Uniendose con codigo {code}...");
        SetLoading(true);
        SetButtonsInteractable(false);

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(joinAllocation.ToRelayServerData("dtls"));

            NetworkManager.Singleton.StartClient();
            Debug.Log($"[Relay] Uniendose con codigo: {code}");
        }
        catch (System.Exception e)
        {
            SetStatus($"Error al unirse: {e.Message}");
            SetLoading(false);
            SetButtonsInteractable(true);
            Debug.LogError($"[Relay] Error: {e}");
        }
    }

    // =========================
    // EVENTOS DE RED
    // =========================

    private void OnServerStarted()
    {
        SetStatus("Servidor iniciado.");
        SetLoading(false);

        // Guardar código de sala para mostrarlo en el HUD durante la partida
        string code = PlayerPrefs.GetString("RoomCode", "");
        if (!string.IsNullOrEmpty(code))
        {
            PlayerPrefs.SetString("PendingRoomCode", code);
            PlayerPrefs.DeleteKey("RoomCode");
        }
        else
        {
            string lanIP = PlayerPrefs.GetString("LANHostIP", "");
            string lanPort = PlayerPrefs.GetString("LANHostPort", DEFAULT_PORT.ToString());
            if (!string.IsNullOrEmpty(lanIP))
            {
                PlayerPrefs.SetString("PendingRoomCode", $"{lanIP}:{lanPort}");
                PlayerPrefs.DeleteKey("LANHostIP");
                PlayerPrefs.DeleteKey("LANHostPort");
            }
        }

        // El host carga CharacterSelect
        Debug.Log("[Network] Host iniciado. Cargando CharacterSelect...");
        UnityEngine.SceneManagement.SceneManager.LoadScene(characterSelectScene);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            SetStatus("Conectado!");
            SetLoading(false);
            // El cliente espera que el host cargue CharacterSelect via NGO SceneManager
            Debug.Log("[Network] Cliente conectado. Esperando escena del host...");
        }
    }

    private void OnBackClicked()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuScene);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId ||
            !NetworkManager.Singleton.IsServer)
        {
            SetStatus("Desconectado del servidor");
            SetLoading(false);
            SetButtonsInteractable(true);
            HideRoomCode();
            ShowMenu();
        }
    }

    // =========================
    // ROOM CODE HUD
    // =========================

    private void ShowRoomCode(string code)
    {
        if (roomCodePanel != null) roomCodePanel.SetActive(true);
        if (roomCodeText != null) roomCodeText.text = $"Codigo de sala:\n{code}";
        Debug.Log($"[Relay] Codigo de sala visible en HUD: {code}");
    }

    private void HideRoomCode()
    {
        if (roomCodePanel != null) roomCodePanel.SetActive(false);
        if (roomCodeText != null) roomCodeText.text = "";
    }

    // =========================
    // HELPERS
    // =========================

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private ushort ParsePort()
    {
        if (lanPortInput != null &&
            ushort.TryParse(lanPortInput.text, out ushort port))
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
        if (lanHostButton != null) lanHostButton.interactable = interactable;
        if (lanJoinButton != null) lanJoinButton.interactable = interactable;
        if (onlineHostButton != null) onlineHostButton.interactable = interactable;
        if (onlineJoinButton != null) onlineJoinButton.interactable = interactable;
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