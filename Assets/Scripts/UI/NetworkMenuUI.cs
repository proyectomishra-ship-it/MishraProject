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

    [Header("Iniciar partida (visible cuando hay jugadores conectados)")]
    [SerializeField] private Button startGameButton;

    [Header("Config")]
    [SerializeField] private int maxPlayers = 6;
    [SerializeField] private string characterSelectScene = "CharacterSelect";
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private Button backButton;

    private const string DEFAULT_IP = "127.0.0.1";
    private const ushort DEFAULT_PORT = 7777;

    private bool servicesInitialized = false;

    // =====================================================================
    // BUG FIX #1 — async void Start() con inicialización de Unity Services
    //
    // PROBLEMA ORIGINAL:
    //   servicesInitialized comienza en false. UnityServices.InitializeAsync()
    //   es una llamada async que puede tardar 1-3 segundos. Si el usuario llega
    //   desde MainMenu y hace clic rápido en "Online Host" antes de que termine,
    //   servicesInitialized sigue siendo false y OnRelayHostClicked() retorna
    //   silenciosamente (solo cambia el statusText brevemente sin feedback claro).
    //
    //   Adicionalmente, en el Unity Editor, Unity Services usa singletons
    //   estáticos que NO se reinician entre sesiones Play. Si el developer
    //   probó primero desde NetworkMenu, las sesiones siguientes desde
    //   MainMenu llaman a InitializeAsync() sobre un SDK ya inicializado,
    //   lo que en algunas versiones lanza ServicesInitializationException y
    //   en otras simplemente no completa bien la auth, dejando
    //   servicesInitialized = false de forma consistente.
    //
    // FIX APLICADO:
    //   1. Deshabilitar botones Online al inicio, habilitarlos solo al completar.
    //   2. Verificar UnityServices.State antes de llamar InitializeAsync().
    //   3. Verificar el estado de autenticación antes de llamar SignIn.
    // =====================================================================
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
        // startGameButton ya no se usa - la lógica de inicio pasó a ClassSelectionUI

        if (lanIPInput != null) lanIPInput.text = DEFAULT_IP;
        if (lanPortInput != null) lanPortInput.text = DEFAULT_PORT.ToString();
        if (playerNameInput != null) playerNameInput.text = "Jugador";

        SetLoading(false);
        HideRoomCode();
        ShowMenu();
        ShowInitialState();

        // FIX: Deshabilitar botones Online hasta que los servicios estén listos.
        // Así el usuario no puede presionar antes de que esté inicializado,
        // sin importar desde qué escena venga.
        SetOnlineButtonsInteractable(false);
        SetStatus("Inicializando servicios online...");

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
        else
        {
            Debug.LogError("[NetworkMenuUI] NetworkManager.Singleton es null.");
        }

        try
        {
            // FIX: Verificar estado antes de inicializar para evitar
            // ServicesInitializationException en el Editor (el SDK mantiene
            // estado estático entre sesiones Play).
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            // FIX: Verificar si ya está autenticado (puede pasar en el Editor
            // entre sesiones o si viene de otra escena).
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            servicesInitialized = true;

            // FIX: Solo habilitar los botones Online DESPUÉS de que los
            // servicios están confirmados como listos.
            SetOnlineButtonsInteractable(true);
            SetStatus("Listo.");
            Debug.Log($"[Relay] Autenticado. PlayerID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (System.Exception e)
        {
            servicesInitialized = false;
            // Botones Online se quedan deshabilitados (ya están así por defecto).
            SetStatus("Modo LAN disponible. (Relay no disponible)");
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
        if (playerNameInput != null) playerNameInput.gameObject.SetActive(false);
        if (tabLANButton != null) tabLANButton.gameObject.SetActive(false);
        if (tabOnlineButton != null) tabOnlineButton.gameObject.SetActive(false);

        if (panelLAN != null) panelLAN.SetActive(lan);
        if (panelOnline != null) panelOnline.SetActive(!lan);
    }

    private void ShowInitialState()
    {
        if (playerNameInput != null) playerNameInput.gameObject.SetActive(true);
        if (tabLANButton != null) tabLANButton.gameObject.SetActive(true);
        if (tabOnlineButton != null) tabOnlineButton.gameObject.SetActive(true);
        if (panelLAN != null) panelLAN.SetActive(false);
        if (panelOnline != null) panelOnline.SetActive(false);
    }

    // =========================
    // LAN
    // =========================

    private void OnLANHostClicked()
    {
        if (NetworkManager.Singleton.IsListening)
        {
            SetStatus("Ya existe una sesión activa. Reiniciá el juego.");
            return;
        }

        ushort port = ParsePort();
        ConfigureTransportLAN("0.0.0.0", port);

        string ip = GetLocalIP();
        PlayerPrefs.SetString("LANHostIP", ip);
        PlayerPrefs.SetString("LANHostPort", port.ToString());

        SetStatus("Iniciando servidor LAN...");
        SetLoading(true);
        SetButtonsInteractable(false);

        bool started = NetworkManager.Singleton.StartHost();
        if (!started)
        {
            SetStatus("Error: no se pudo iniciar el servidor LAN.");
            SetLoading(false);
            SetButtonsInteractable(true);
            Debug.LogError("[LAN] StartHost() retornó false.");
            return;
        }

        // Mostrar IP y puerto para que los clientes puedan conectarse
        if (joinCodeDisplay != null)
            joinCodeDisplay.text = $"IP: {ip}\nPuerto: {port}";

        SetStatus("Servidor LAN iniciado. Compartí la IP con los jugadores.");
    }

    private void OnLANJoinClicked()
    {
        if (NetworkManager.Singleton.IsListening)
        {
            SetStatus("Ya existe una sesión activa.");
            return;
        }

        string ip = lanIPInput != null ? lanIPInput.text.Trim() : DEFAULT_IP;
        ushort port = ParsePort();

        if (string.IsNullOrWhiteSpace(ip))
        {
            SetStatus("Ingresá una IP válida.");
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
        // Este guard ahora es solo una salvaguarda extra.
        // En condiciones normales los botones ya están deshabilitados
        // hasta que servicesInitialized = true.
        if (!servicesInitialized)
        {
            SetStatus("Servicios online no disponibles. Usá LAN.");
            return;
        }

        // =====================================================================
        // BUG FIX #2 — Retorno silencioso sin feedback
        //
        // PROBLEMA ORIGINAL:
        //   if (NetworkManager.Singleton.IsListening) return;
        //   No había ningún mensaje ni indicador visual. Si el NetworkManager
        //   ya estaba activo (ej: DontDestroyOnLoad de una sesión previa que
        //   no se cerró correctamente), el botón simplemente no hacía nada.
        //
        // FIX: Mostrar mensaje claro al usuario.
        // =====================================================================
        if (NetworkManager.Singleton.IsListening)
        {
            SetStatus("Ya hay una sesión activa. Usá el botón Volver para salir.");
            Debug.LogWarning("[Relay] StartHost ignorado: NetworkManager ya está escuchando.");
            return;
        }

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

            PlayerPrefs.SetString("RoomCode", joinCode);

            // FIX: Verificar retorno de StartHost()
            // Mostrar el código antes de iniciar para que el host lo vea
            if (joinCodeDisplay != null)
                joinCodeDisplay.text = $"Código: {joinCode}";

            SetStatus($"Código de sala: {joinCode}\nCompartilo con los demás jugadores.");
            Debug.Log($"[Relay] Código generado: {joinCode}");

            bool started = NetworkManager.Singleton.StartHost();
            if (!started)
            {
                SetStatus("Error: no se pudo iniciar el host Relay.");
                SetLoading(false);
                SetButtonsInteractable(true);
                Debug.LogError("[Relay] StartHost() retornó false.");
            }
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
            SetStatus("Servicios online no disponibles. Usá LAN.");
            return;
        }

        if (NetworkManager.Singleton.IsListening)
        {
            SetStatus("Ya hay una sesión activa.");
            return;
        }

        string code = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpper() : "";

        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("Ingresá el código de sala.");
            return;
        }

        SetStatus($"Uniéndose con código {code}...");
        SetLoading(true);
        SetButtonsInteractable(false);

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(joinAllocation.ToRelayServerData("dtls"));

            NetworkManager.Singleton.StartClient();
            Debug.Log($"[Relay] Uniéndose con código: {code}");
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
        SetLoading(false);
        Debug.Log("[Network] Host iniciado. Cargando CharacterSelect como lobby...");

        // Guardar código para el HUD
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

        // CharacterSelect actúa como lobby — el host inicia partida desde ahí
        NetworkManager.Singleton.SceneManager.LoadScene(
            characterSelectScene,
            UnityEngine.SceneManagement.LoadSceneMode.Single
        );
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            SetStatus("¡Conectado! Esperando escena del host...");
            SetLoading(false);
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
            SetStatus("Desconectado del servidor.");
            SetLoading(false);
            SetButtonsInteractable(true);
            SetOnlineButtonsInteractable(servicesInitialized);
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
        if (roomCodeText != null) roomCodeText.text = $"Código de sala:\n{code}";
        Debug.Log($"[Relay] Código de sala visible en HUD: {code}");
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

    /// <summary>
    /// Controla los 4 botones de acción (LAN + Online).
    /// </summary>
    private void SetButtonsInteractable(bool interactable)
    {
        if (lanHostButton != null) lanHostButton.interactable = interactable;
        if (lanJoinButton != null) lanJoinButton.interactable = interactable;
        if (interactable)
            SetOnlineButtonsInteractable(servicesInitialized);
        else
            SetOnlineButtonsInteractable(false);
    }

    /// <summary>
    /// FIX: Control separado para botones Online.
    /// Permite deshabilitar Online independientemente mientras los
    /// servicios se inicializan, sin afectar los botones LAN.
    /// </summary>
    private void SetOnlineButtonsInteractable(bool interactable)
    {
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