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