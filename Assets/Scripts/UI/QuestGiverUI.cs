using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Interfaz del NPC que entrega misiones. Ofrece una misión secundaria al azar
/// entre las disponibles; el jugador puede aceptarla o cerrar.
///
/// Uso: agregar este componente a cualquier objeto de la escena (por ejemplo
/// uno vacío "QuestGiverUI"). Si no se asignan referencias, crea su propia
/// interfaz por código. Si querés diseñarla vos, asigná las referencias.
/// </summary>
public class QuestGiverUI : MonoBehaviour
{
    public static QuestGiverUI Instance { get; private set; }

    [Header("Interfaz propia (opcional)")]
    [Tooltip("Si se deja vacío se crea una interfaz por defecto.")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text objectivesText;
    [SerializeField] private TMP_Text rewardsText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text promptText;

    public bool IsOpen { get; private set; }
    public QuestGiverNPC CurrentGiver { get; private set; }

    private QuestData offeredQuest;
    private Player localPlayer;
    private PlayerCameraBinder cameraBinder;
    private QuestGiverNPC promptOwner;

    private static Player cachedLocalPlayer;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (panel == null)
            BuildDefaultUI();

        if (acceptButton != null)
        {
            acceptButton.onClick.RemoveAllListeners();
            acceptButton.onClick.AddListener(OnAcceptClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        panel.SetActive(false);

        if (promptText != null)
            promptText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (NetworkQuestManager.Instance != null)
            NetworkQuestManager.Instance.OnQuestDataChanged -= HandleQuestDataChanged;
    }

    private void Update()
    {
        if (IsOpen &&
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    // =========================================================
    // JUGADOR LOCAL
    // =========================================================

    public static Player GetLocalPlayer()
    {
        if (cachedLocalPlayer != null)
            return cachedLocalPlayer;

        NetworkManager nm = NetworkManager.Singleton;

        if (nm == null || !nm.IsClient || nm.LocalClient == null)
            return null;

        NetworkObject playerObject = nm.LocalClient.PlayerObject;

        if (playerObject == null)
            return null;

        cachedLocalPlayer = playerObject.GetComponent<Player>();
        return cachedLocalPlayer;
    }

    // =========================================================
    // PROMPT "Presioná E"
    // =========================================================

    public void SetPrompt(QuestGiverNPC owner, string text)
    {
        promptOwner = owner;

        if (promptText == null)
            return;

        promptText.text = text;

        if (!promptText.gameObject.activeSelf)
            promptText.gameObject.SetActive(true);
    }

    public void ClearPrompt(QuestGiverNPC owner)
    {
        if (promptOwner != owner)
            return;

        promptOwner = null;

        if (promptText != null && promptText.gameObject.activeSelf)
            promptText.gameObject.SetActive(false);
    }

    // =========================================================
    // ABRIR / CERRAR
    // =========================================================

    public void Open(QuestGiverNPC giver)
    {
        if (IsOpen)
            return;

        if (NetworkQuestManager.Instance == null)
        {
            Debug.LogWarning("[QuestGiverUI] No hay NetworkQuestManager en la escena.");
            return;
        }

        localPlayer = GetLocalPlayer();

        if (localPlayer == null)
            return;

        cameraBinder = localPlayer.GetComponent<PlayerCameraBinder>();

        CurrentGiver = giver;
        IsOpen = true;

        if (promptText != null)
            promptText.gameObject.SetActive(false);

        if (npcNameText != null)
            npcNameText.text = giver != null ? giver.NpcName : string.Empty;

        ShowOffer();

        panel.SetActive(true);

        // Liberar el cursor y congelar movimiento / cámara (igual que el inventario).
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        cameraBinder?.SetLookEnabled(false);
        localPlayer.SetInputBlocked(true);

        NetworkQuestManager.Instance.OnQuestDataChanged += HandleQuestDataChanged;
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        CurrentGiver = null;
        offeredQuest = null;

        panel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        cameraBinder?.SetLookEnabled(true);
        localPlayer?.SetInputBlocked(false);

        if (NetworkQuestManager.Instance != null)
            NetworkQuestManager.Instance.OnQuestDataChanged -= HandleQuestDataChanged;
    }

    // =========================================================
    // OFERTA
    // =========================================================

    private void ShowOffer()
    {
        NetworkQuestManager manager = NetworkQuestManager.Instance;

        offeredQuest = null;

        if (!manager.HasFreeSideQuestSlot())
        {
            ShowMessage("Ya tienen una misión secundaria en curso.\nTerminenla antes de pedir otra.");
            return;
        }

        if (!manager.TryGetRandomAvailableSideQuest(out QuestData quest))
        {
            ShowMessage("Por ahora no tengo más trabajos para ustedes.");
            return;
        }

        offeredQuest = quest;

        SetOfferVisible(true);

        if (questNameText != null) questNameText.text = quest.questName;
        if (descriptionText != null) descriptionText.text = quest.description;
        if (objectivesText != null) objectivesText.text = "Objetivos:\n" + QuestTextUtil.Objectives(quest);
        if (rewardsText != null) rewardsText.text = QuestTextUtil.Rewards(quest);
    }

    private void ShowMessage(string message)
    {
        SetOfferVisible(false);

        if (messageText != null)
            messageText.text = message;
    }

    private void SetOfferVisible(bool visible)
    {
        if (questNameText != null) questNameText.gameObject.SetActive(visible);
        if (descriptionText != null) descriptionText.gameObject.SetActive(visible);
        if (objectivesText != null) objectivesText.gameObject.SetActive(visible);
        if (rewardsText != null) rewardsText.gameObject.SetActive(visible);
        if (acceptButton != null) acceptButton.gameObject.SetActive(visible);
        if (messageText != null) messageText.gameObject.SetActive(!visible);
    }

    // Si otro jugador acepta la misión que estoy viendo, ofrecer otra.
    private void HandleQuestDataChanged()
    {
        if (!IsOpen || offeredQuest == null)
            return;

        NetworkQuestManager manager = NetworkQuestManager.Instance;

        if (manager != null &&
            manager.TryGetQuestState(offeredQuest.questID, out NetworkQuestState state) &&
            state.State != QuestState.Available)
        {
            ShowOffer();
        }
    }

    private void OnAcceptClicked()
    {
        if (offeredQuest == null || NetworkQuestManager.Instance == null)
            return;

        NetworkQuestManager.Instance.RequestAcceptQuest(offeredQuest.questID);

        Close();
    }

    // =========================================================
    // INTERFAZ POR DEFECTO
    // =========================================================

    private void BuildDefaultUI()
    {
        var canvasGo = new GameObject(
            "QuestGiverCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Aviso "Presioná E"
        promptText = CreateText(
            canvasGo.transform, "PromptText", 30f, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(900f, 60f));

        // Panel
        panel = new GameObject("QuestGiverPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        panel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.92f);

        Place(panel.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 600f));

        Transform p = panel.transform;
        Vector2 top = new Vector2(0.5f, 1f);

        npcNameText = CreateText(p, "NpcName", 26f, TextAlignmentOptions.Center,
            top, new Vector2(0f, -20f), new Vector2(740f, 40f));
        npcNameText.color = new Color(0.8f, 0.8f, 0.8f);

        questNameText = CreateText(p, "QuestName", 40f, TextAlignmentOptions.Center,
            top, new Vector2(0f, -70f), new Vector2(740f, 60f));
        questNameText.color = new Color(1f, 0.85f, 0.3f);

        descriptionText = CreateText(p, "Description", 26f, TextAlignmentOptions.TopLeft,
            top, new Vector2(0f, -140f), new Vector2(740f, 150f));

        objectivesText = CreateText(p, "Objectives", 26f, TextAlignmentOptions.TopLeft,
            top, new Vector2(0f, -300f), new Vector2(740f, 130f));

        rewardsText = CreateText(p, "Rewards", 26f, TextAlignmentOptions.TopLeft,
            top, new Vector2(0f, -440f), new Vector2(740f, 50f));
        rewardsText.color = new Color(0.6f, 1f, 0.6f);

        messageText = CreateText(p, "Message", 30f, TextAlignmentOptions.Center,
            top, new Vector2(0f, -180f), new Vector2(700f, 200f));

        acceptButton = CreateButton(p, "AcceptButton", "Aceptar", new Vector2(-130f, 50f));
        closeButton = CreateButton(p, "CloseButton", "Cerrar", new Vector2(130f, 50f));
    }

    private static TMP_Text CreateText(
        Transform parent, string name, float size, TextAlignmentOptions alignment,
        Vector2 anchor, Vector2 position, Vector2 dimensions)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        Place(go.GetComponent<RectTransform>(), anchor, position, dimensions);

        return text;
    }

    private static Button CreateButton(
        Transform parent, string name, string label, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 1f);

        Place(go.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0f), position, new Vector2(220f, 60f));

        TMP_Text text = CreateText(go.transform, "Label", 28f, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 60f));

        text.text = label;

        return go.GetComponent<Button>();
    }

    // Ancla y pivote en el mismo punto: la posición es relativa a ese ancla.
    private static void Place(
        RectTransform rect, Vector2 anchor, Vector2 position, Vector2 dimensions)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
    }
}
