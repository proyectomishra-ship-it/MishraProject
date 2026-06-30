using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Lobby de selección de clase. Funciona como pantalla intermedia donde:
///   - Todos los jugadores eligen su clase
///   - El HOST ve "Iniciar Partida" y decide cuándo arrancar
///   - Los CLIENTES ven "Listo" y esperan al host
/// </summary>
public class ClassSelectionUI : NetworkBehaviour
{
    [Header("Botones de clase")]
    [SerializeField] private Button warriorButton;
    [SerializeField] private Button mageButton;
    [SerializeField] private Button hunterButton;

    [Header("UI")]
    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI selectedClassText;
    [SerializeField] private GameObject waitingPanel;

    [Header("Código de sala (solo visible para el host)")]
    [SerializeField] private GameObject roomCodePanel;
    [SerializeField] private TextMeshProUGUI roomCodeText;

    [Header("Escena de juego")]
    [SerializeField] private string gameSceneName = "Scene1";

    private NetworkVariable<int> readyCount = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // FIX: Cuántos jugadores hay conectados.
    // En versiones anteriores esto se fijaba UNA SOLA VEZ en OnNetworkSpawn(),
    // cuando el cliente de Relay todavía no había llegado → totalPlayers = 1.
    // Ahora se actualiza en tiempo real vía OnClientConnectedCallback.
    private NetworkVariable<int> totalPlayers = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private string selectedClass = "";
    private bool isReady = false;

    private void Start()
    {
        warriorButton?.onClick.AddListener(() => SelectClass("Warrior"));
        mageButton?.onClick.AddListener(() => SelectClass("Mage"));
        hunterButton?.onClick.AddListener(() => SelectClass("Hunter"));
        readyButton?.onClick.AddListener(OnReadyClicked);

        if (readyButton != null) readyButton.interactable = false;
        if (waitingPanel != null) waitingPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetStatus("Elegí tu clase para continuar.");
        MostrarCodigoDeSala();
    }

    private void MostrarCodigoDeSala()
    {
        string code = PlayerPrefs.GetString("PendingRoomCode", "");

        if (!string.IsNullOrEmpty(code))
        {
            if (roomCodePanel != null) roomCodePanel.SetActive(true);

            if (roomCodeText != null)
            {
                bool esLAN = code.Contains(":");
                if (esLAN)
                    roomCodeText.text = $"Conectar por LAN:\n<b>{code}</b>\n(IP : Puerto)";
                else
                    roomCodeText.text = $"Código de sala online:\n<b>{code}</b>\n(Compartilo con los jugadores)";
            }

            PlayerPrefs.DeleteKey("PendingRoomCode");
            PlayerPrefs.Save();

            Debug.Log($"[ClassSelect] Código de sala mostrado al host: {code}");
        }
        else
        {
            if (roomCodePanel != null) roomCodePanel.SetActive(false);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        readyCount.OnValueChanged += OnReadyCountChanged;
        totalPlayers.OnValueChanged += OnTotalPlayersChanged;

        if (IsServer)
        {
            // Contar jugadores actuales (incluye el host)
            totalPlayers.Value = NetworkManager.Singleton.ConnectedClients.Count;

            // FIX: Relay — el cliente se conecta DESPUÉS de que se cargó CharacterSelect,
            // por lo que OnNetworkSpawn ya disparó con Count = 1.
            // Ahora escuchamos las conexiones futuras para actualizar totalPlayers en tiempo real.
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            Debug.Log($"[ClassSelect] Lobby iniciado con {totalPlayers.Value} jugador(es).");
        }

        if (readyButtonText != null)
            readyButtonText.text = IsHost ? "Iniciar Partida" : "Listo";
    }

    public override void OnNetworkDespawn()
    {
        readyCount.OnValueChanged -= OnReadyCountChanged;
        totalPlayers.OnValueChanged -= OnTotalPlayersChanged;

        // FIX: desuscribir siempre (aunque NetworkManager ya esté destruyéndose, es seguro)
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // ─── Callbacks de conexión (solo servidor) ────────────────────────────────

    /// <summary>
    /// FIX: Se llama cuando un cliente nuevo se conecta al host.
    /// Actualiza totalPlayers para que ConfirmClassServerRpc espere a todos.
    /// </summary>
    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        totalPlayers.Value = NetworkManager.Singleton.ConnectedClients.Count;
        Debug.Log($"[ClassSelect] Nuevo cliente: {clientId}. Total jugadores: {totalPlayers.Value}");
    }

    /// <summary>
    /// Si un jugador se desconecta antes de confirmar, ajustar contadores para
    /// que el juego no quede bloqueado esperando a alguien que ya no está.
    /// </summary>
    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        int newTotal = Mathf.Max(1, NetworkManager.Singleton.ConnectedClients.Count);
        totalPlayers.Value = newTotal;

        // Si el que se fue ya había confirmado y ahora readyCount >= totalPlayers → iniciar
        if (readyCount.Value >= totalPlayers.Value && readyCount.Value > 0)
            LoadGameScene();

        Debug.Log($"[ClassSelect] Cliente desconectado: {clientId}. Total: {totalPlayers.Value}");
    }

    // ─── Selección de clase ───────────────────────────────────────────────────

    private void SelectClass(string className)
    {
        if (isReady) return;

        selectedClass = className;
        PlayerPrefs.SetString("SelectedClass", className);

        if (selectedClassText != null)
            selectedClassText.text = $"Clase seleccionada: {className}";

        if (readyButton != null) readyButton.interactable = true;

        SetStatus($"Seleccionaste {className}. " +
                  (IsHost ? "Podés iniciar cuando todos estén listos."
                           : "Confirmá cuando estés listo."));
    }

    private void OnReadyClicked()
    {
        if (string.IsNullOrEmpty(selectedClass)) return;
        if (isReady) return;

        isReady = true;
        if (readyButton != null) readyButton.interactable = false;
        if (waitingPanel != null) waitingPanel.SetActive(true);

        if (IsHost)
            SetStatus("Iniciando partida...");
        else
            SetStatus("Esperando a los demás jugadores...");

        if (IsSpawned)
        {
            ConfirmClassServerRpc(selectedClass);
        }
        else
        {
            // Fallback: host sin NGO spawneado aún
            ulong localId = NetworkManager.Singleton?.LocalClientId ?? 0;
            GameSessionData.Instance?.SetPlayerClass(localId, selectedClass);
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
        }
    }

    [Rpc(SendTo.Server)]
    private void ConfirmClassServerRpc(string className, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        GameSessionData.Instance?.SetPlayerClass(clientId, className);

        readyCount.Value++;

        Debug.Log($"[ClassSelect] {clientId} eligió {className}. " +
                  $"Listos: {readyCount.Value}/{totalPlayers.Value}");

        if (readyCount.Value >= totalPlayers.Value)
            LoadGameScene();
    }

    private void LoadGameScene()
    {
        if (!IsServer) return;
        Debug.Log("[ClassSelect] Iniciando partida...");
        NetworkManager.Singleton.SceneManager.LoadScene(
            gameSceneName,
            UnityEngine.SceneManagement.LoadSceneMode.Single
        );
    }

    // ─── Callbacks de NetworkVariables ────────────────────────────────────────

    private void OnReadyCountChanged(int oldVal, int newVal)
    {
        int total = totalPlayers.Value;
        if (IsHost)
            SetStatus($"Jugadores listos: {newVal}/{total}. " +
                      (newVal > 0 ? "Podés iniciar." : "Esperando jugadores..."));
        else
            SetStatus($"Jugadores listos: {newVal}/{total}");
    }

    private void OnTotalPlayersChanged(int oldVal, int newVal)
    {
        if (!IsHost) return;
        SetStatus($"Jugadores conectados: {newVal}. Elegí tu clase e iniciá cuando estés listo.");
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }
}