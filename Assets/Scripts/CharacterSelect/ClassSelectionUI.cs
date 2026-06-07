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

    // =========================================================================
    // FIX: roomCodePanel + roomCodeText
    //
    // PROBLEMA ORIGINAL:
    //   NetworkMenuUI generaba el join code (Relay) o la IP:Puerto (LAN) y los
    //   guardaba en PlayerPrefs["PendingRoomCode"] antes de cargar esta escena.
    //   Sin embargo, ClassSelectionUI nunca leía ese valor, por lo que el host
    //   no podía ver el código para compartirlo con los demás jugadores.
    //   El joinCodeDisplay de NetworkMenuUI quedaba visible durante un frame o
    //   dos antes del cambio de escena, haciendo el código ilegible en la práctica.
    //
    // FIX APLICADO:
    //   1. Añadir roomCodePanel (GameObject) y roomCodeText (TMP) serializados.
    //   2. En Start(), leer PlayerPrefs["PendingRoomCode"].
    //   3. Si existe y somos host → mostrar el panel con el código.
    //   4. Si no existe o somos cliente → ocultar el panel.
    //   5. Limpiar la clave de PlayerPrefs para evitar que persista entre partidas.
    //
    // SETUP EN UNITY INSPECTOR (CharacterSelect scene):
    //   a) Crear un GameObject "RoomCodePanel" hijo del Canvas.
    //   b) Darle un fondo visible (Image con color semitransparente, por ejemplo).
    //   c) Añadir un hijo TextMeshProUGUI llamado "RoomCodeText".
    //   d) Asignar ambos en los campos de abajo.
    //   e) Dejar el panel ACTIVO en el Editor; el script lo oculta si no hay código.
    // =========================================================================
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

    // Cuántos jugadores hay conectados cuando se cargó esta escena
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

        // FIX: Leer el código de sala guardado por NetworkMenuUI y mostrarlo.
        // NetworkMenuUI guarda el join code (Relay) o la IP:Puerto (LAN) en
        // PlayerPrefs["PendingRoomCode"] justo antes de cargar esta escena.
        // Start() corre después de que los PlayerPrefs ya están disponibles,
        // así que podemos leerlo aquí de forma segura.
        MostrarCodigoDeSala();
    }

    // =========================================================================
    // FIX: método que lee y muestra el código de sala al host.
    // Si no hay código (cliente) o no hay TextMeshPro asignado, oculta el panel.
    // =========================================================================
    private void MostrarCodigoDeSala()
    {
        string code = PlayerPrefs.GetString("PendingRoomCode", "");

        if (!string.IsNullOrEmpty(code))
        {
            // Hay un código → somos el host (los clientes no tienen esta clave).
            if (roomCodePanel != null) roomCodePanel.SetActive(true);

            if (roomCodeText != null)
            {
                // Detectar si es Relay (no contiene ':') o LAN (IP:Puerto).
                bool esLAN = code.Contains(":");
                if (esLAN)
                    roomCodeText.text = $"Conectar por LAN:\n<b>{code}</b>\n(IP : Puerto)";
                else
                    roomCodeText.text = $"Código de sala online:\n<b>{code}</b>\n(Compartilo con los jugadores)";
            }

            // Limpiar PlayerPrefs para que no persista en la próxima sesión.
            PlayerPrefs.DeleteKey("PendingRoomCode");
            PlayerPrefs.Save();

            Debug.Log($"[ClassSelect] Código de sala mostrado al host: {code}");
        }
        else
        {
            // Sin código → somos cliente o el valor ya se leyó.
            if (roomCodePanel != null) roomCodePanel.SetActive(false);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        readyCount.OnValueChanged += OnReadyCountChanged;
        totalPlayers.OnValueChanged += OnTotalPlayersChanged;

        // El host registra el total de jugadores conectados
        if (IsServer)
            totalPlayers.Value = NetworkManager.Singleton.ConnectedClients.Count;

        // Cambiar texto del botón según si es host o cliente
        if (readyButtonText != null)
            readyButtonText.text = IsHost ? "Iniciar Partida" : "Listo";
    }

    public override void OnNetworkDespawn()
    {
        readyCount.OnValueChanged -= OnReadyCountChanged;
        totalPlayers.OnValueChanged -= OnTotalPlayersChanged;
    }

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

        // El host puede iniciar aunque no todos estén listos (es su decisión)
        // Solo forzar inicio cuando TODOS confirmen
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