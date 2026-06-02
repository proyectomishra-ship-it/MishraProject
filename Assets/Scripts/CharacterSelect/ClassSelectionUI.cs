using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Pantalla de selección de clase. Vive en la escena CharacterSelect.
/// Cada jugador elige su clase y confirma. Cuando todos confirman,
/// el host carga la escena de juego.
///
/// SETUP EN UNITY:
///   - Crear escena CharacterSelect
///   - Agregar este script a un GameObject vacío
///   - Asignar los botones y el panel en el Inspector
/// </summary>
public class ClassSelectionUI : NetworkBehaviour
{
    [Header("Botones de clase")]
    [SerializeField] private Button warriorButton;
    [SerializeField] private Button mageButton;
    [SerializeField] private Button hunterButton;

    [Header("UI")]
    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI selectedClassText;
    [SerializeField] private GameObject waitingPanel;

    [Header("Escena de juego")]
    [SerializeField] private string gameSceneName = "Scene1";

    // Cuántos jugadores confirmaron su clase
    private NetworkVariable<int> readyCount = new NetworkVariable<int>(
        0,
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

        // Liberar el cursor para que el jugador pueda clickear los botones
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetStatus("Elegí tu clase para continuar.");
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        readyCount.OnValueChanged += OnReadyCountChanged;
    }

    public override void OnNetworkDespawn()
    {
        readyCount.OnValueChanged -= OnReadyCountChanged;
    }

    private void SelectClass(string className)
    {
        if (isReady) return;

        selectedClass = className;
        PlayerPrefs.SetString("SelectedClass", className);

        if (selectedClassText != null)
            selectedClassText.text = $"Clase seleccionada: {className}";

        if (readyButton != null) readyButton.interactable = true;

        SetStatus($"Seleccionaste {className}. Confirmá cuando estés listo.");
        Debug.Log($"[ClassSelect] Clase elegida: {className}");
    }

    private void OnReadyClicked()
    {
        if (string.IsNullOrEmpty(selectedClass)) return;
        if (isReady) return;

        isReady = true;
        if (readyButton != null) readyButton.interactable = false;
        if (waitingPanel != null) waitingPanel.SetActive(true);

        SetStatus("Esperando a los demás jugadores...");

        // Si el NetworkObject ya está spawneado usar RPC,
        // si no (host jugando solo) guardar directamente en GameSessionData
        if (IsSpawned)
        {
            ConfirmClassServerRpc(selectedClass);
        }
        else
        {
            // Fallback para host solo o cuando NGO aún no spawneó el objeto
            ulong localId = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.LocalClientId
                : 0;

            if (GameSessionData.Instance != null)
                GameSessionData.Instance.SetPlayerClass(localId, selectedClass);

            // Si somos el único jugador, cargar la escena directamente
            if (NetworkManager.Singleton == null ||
                NetworkManager.Singleton.ConnectedClients.Count <= 1)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void ConfirmClassServerRpc(string className, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        // Guardar la clase del cliente en el GameSessionData
        GameSessionData.Instance.SetPlayerClass(clientId, className);

        readyCount.Value++;

        int totalPlayers = NetworkManager.Singleton.ConnectedClients.Count;
        Debug.Log($"[ClassSelect] Cliente {clientId} eligió {className}. " +
                  $"Listos: {readyCount.Value}/{totalPlayers}");

        // Cuando todos están listos, cargar la escena de juego
        if (readyCount.Value >= totalPlayers)
            LoadGameScene();
    }

    private void LoadGameScene()
    {
        if (!IsServer) return;
        Debug.Log("[ClassSelect] Todos listos. Cargando partida...");
        NetworkManager.Singleton.SceneManager.LoadScene(
            gameSceneName,
            UnityEngine.SceneManagement.LoadSceneMode.Single
        );
    }

    private void OnReadyCountChanged(int oldVal, int newVal)
    {
        int total = NetworkManager.Singleton.ConnectedClients.Count;
        SetStatus($"Jugadores listos: {newVal}/{total}");
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }
}