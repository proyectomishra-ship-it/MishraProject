using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Cartel "Elige una mejora" con 3 opciones. Se crea solo por código (sin prefabs), igual que SkillNoticeUI,
/// y toma colores, bordes y tipografías de UITheme para verse como el resto de la interfaz.
///
/// Cómo funciona ahora:
///  - Al aparecer libera el mouse, congela la cámara y bloquea el movimiento/ataques del jugador
///    (lo mismo que hace el inventario) y se elige con CLICK. Las teclas 1-2-3 siguen funcionando como atajo.
///  - Si estás jugando solo (host sin otros jugadores) además pausa la partida (Time.timeScale = 0).
///    En multijugador NO se pausa, porque los demás seguirían jugando: solo se bloquea tu personaje.
///  - Si en ese momento hay otra ventana abierta (inventario, misiones) o el jugador está muerto,
///    el cartel espera y aparece apenas se libere.
///
/// Uso (lo llama PlayerSkillUpgrades): SkillUpgradeOfferUI.Show(...) / SkillUpgradeOfferUI.Hide().
/// </summary>
public class SkillUpgradeOfferUI : MonoBehaviour
{
    // ---- Ajustes rápidos de posición y tamaño ----
    private static readonly Vector2 ScreenAnchor = new Vector2(0.5f, 0.5f);
    private const float CardWidth = 290f;
    private const float CardHeight = 180f;
    private const float DimAlpha = 0.5f;             // oscurecido del fondo mientras se elige
    private const float InputGraceSeconds = 0.25f;   // evita elegir sin querer al aparecer
    private const float ServerWaitSeconds = 1.5f;    // espera máxima a la respuesta del servidor

    private static readonly Key[] ChoiceKeys = { Key.Digit1, Key.Digit2, Key.Digit3 };

    private static SkillUpgradeOfferUI instance;

    /// <summary>true mientras el cartel está en pantalla. InventoryUI lo usa para no abrirse encima.</summary>
    public static bool IsOpen => instance != null && instance.showing;

    private sealed class Card
    {
        public GameObject root;
        public Image background;
        public Outline outline;
        public Button button;
        public TMP_Text key;
        public TMP_Text title;
        public TMP_Text description;
        public TMP_Text level;
    }

    private PlayerSkillUpgrades owner;
    private Player player;
    private PlayerInputController playerInput;
    private PlayerCameraBinder cameraBinder;

    private GameObject dimRoot;
    private GameObject panelRoot;
    private CanvasGroup group;
    private TMP_Text titleText;
    private TMP_Text pendingText;
    private TMP_Text hintText;
    private readonly Card[] cards = new Card[PlayerSkillUpgrades.OfferSize];

    private int offerCount;
    private float lockUntil;

    private bool hasOffer;     // hay una oferta vigente (puede estar esperando a que se libere el jugador)
    private bool showing;      // el cartel está en pantalla

    private bool timePaused;
    private float previousTimeScale = 1f;

    // =========================================================
    // API
    // =========================================================

    public static void Show(PlayerSkillUpgrades owner, int[] types, int pending)
    {
        if (owner == null || types == null || types.Length == 0 || pending <= 0)
        {
            Hide();
            return;
        }

        EnsureInstance();
        instance.SetOffer(owner, types, pending);
    }

    public static void Hide()
    {
        if (instance != null)
            instance.Close();
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        var go = new GameObject("SkillUpgradeOfferUI");

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 35;   // por debajo de SkillNoticeUI (40)

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        instance = go.AddComponent<SkillUpgradeOfferUI>();
        instance.Build();
    }

    // =========================================================
    // ESTILO (UITheme, con valores de respaldo si no existe el asset)
    // =========================================================

    private static Color PanelColor =>
        UITheme.Current != null ? UITheme.Current.GetFlatColor(UITheme.SpriteRole.Panel) : new Color(0.106f, 0.094f, 0.086f, 0.92f);

    private static Color CardColor =>
        UITheme.Current != null ? UITheme.Current.stoneMid : new Color(0.18f, 0.16f, 0.14f, 1f);

    private static Color CardHoverColor =>
        UITheme.Current != null ? Color.Lerp(UITheme.Current.stoneMid, UITheme.Current.bronze, 0.45f) : new Color(0.42f, 0.33f, 0.2f, 1f);

    private static Color BorderColor =>
        UITheme.Current != null ? UITheme.Current.bronze : new Color(0.66f, 0.47f, 0.23f, 1f);

    private static Color AccentColor =>
        UITheme.Current != null ? UITheme.Current.textAccent : new Color(0.85f, 0.69f, 0.4f, 1f);

    private static Color TextColor =>
        UITheme.Current != null ? UITheme.Current.textPrimary : new Color(0.91f, 0.86f, 0.75f, 1f);

    private static Color MutedColor =>
        UITheme.Current != null ? UITheme.Current.textMuted : new Color(0.61f, 0.57f, 0.49f, 1f);

    private static Sprite ThemeSprite(UITheme.SpriteRole role) =>
        UITheme.Current != null ? UITheme.Current.GetSprite(role) : null;

    // =========================================================
    // CONSTRUCCIÓN (una sola vez)
    // =========================================================

    private void Build()
    {
        // Fondo oscuro a pantalla completa: también evita hacer click en cosas del juego por detrás.
        dimRoot = NewObject("Dim", transform);
        RectTransform dimRect = dimRoot.GetComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = dimRect.offsetMax = Vector2.zero;

        Image dim = dimRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, DimAlpha);
        dim.raycastTarget = true;
        dimRoot.SetActive(false);

        panelRoot = NewObject("Panel", transform);
        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = ScreenAnchor;
        panelRect.pivot = new Vector2(0.5f, 0.5f);

        group = panelRoot.AddComponent<CanvasGroup>();

        Image panelImage = panelRoot.AddComponent<Image>();
        StyleImage(panelImage, PanelColor, ThemeSprite(UITheme.SpriteRole.Panel));
        AddBorder(panelRoot);

        VerticalLayoutGroup vertical = panelRoot.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(20, 20, 14, 14);
        vertical.spacing = 8f;
        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        ContentSizeFitter fitter = panelRoot.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        titleText = NewText("Title", panelRoot.transform, UITheme.TextStyle.Heading, 30f, AccentColor, TextAlignmentOptions.Center);
        titleText.text = "Elige una mejora";
        titleText.fontStyle = FontStyles.Bold;

        pendingText = NewText("Pending", panelRoot.transform, UITheme.TextStyle.Caption, 18f, MutedColor, TextAlignmentOptions.Center);

        // Fila con las 3 tarjetas.
        GameObject row = NewObject("Cards", panelRoot.transform);
        HorizontalLayoutGroup horizontal = row.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 14f;
        horizontal.childAlignment = TextAnchor.MiddleCenter;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = false;

        for (int i = 0; i < cards.Length; i++)
            cards[i] = BuildCard(row.transform, i);

        hintText = NewText("Hint", panelRoot.transform, UITheme.TextStyle.Caption, 16f, MutedColor, TextAlignmentOptions.Center);

        panelRoot.SetActive(false);
    }

    private Card BuildCard(Transform parent, int index)
    {
        var card = new Card();

        card.root = NewObject("Card" + (index + 1), parent);

        LayoutElement size = card.root.AddComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = CardWidth;
        size.minHeight = size.preferredHeight = CardHeight;

        card.background = card.root.AddComponent<Image>();
        StyleImage(card.background, Color.white, ThemeSprite(UITheme.SpriteRole.Slot));

        card.outline = AddBorder(card.root);

        card.button = card.root.AddComponent<Button>();
        card.button.targetGraphic = card.background;
        card.button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = card.button.colors;
        colors.normalColor = CardColor;
        colors.highlightedColor = CardHoverColor;
        colors.selectedColor = CardColor;
        colors.pressedColor = BorderColor;
        colors.disabledColor = CardColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        card.button.colors = colors;

        int slot = index;
        card.button.onClick.AddListener(() => Choose(slot));

        VerticalLayoutGroup layout = card.root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 10, 10);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // Fila superior: nombre de la habilidad a la que pertenece la mejora (+ atajo de teclado).
        card.key = NewText("Skill", card.root.transform, UITheme.TextStyle.Caption, 16f, MutedColor, TextAlignmentOptions.Left);

        card.title = NewText("Name", card.root.transform, UITheme.TextStyle.Heading, 26f, AccentColor, TextAlignmentOptions.Left);
        card.title.fontStyle = FontStyles.Bold;

        card.description = NewText("Description", card.root.transform, UITheme.TextStyle.Body, 20f, TextColor, TextAlignmentOptions.TopLeft);

        card.level = NewText("Level", card.root.transform, UITheme.TextStyle.Caption, 16f, MutedColor, TextAlignmentOptions.Left);

        // La descripción ocupa el espacio sobrante, así el nivel queda siempre abajo.
        LayoutElement descLayout = card.description.gameObject.AddComponent<LayoutElement>();
        descLayout.flexibleHeight = 1f;

        card.root.SetActive(false);
        return card;
    }

    // =========================================================
    // OFERTA: llega del servidor
    // =========================================================

    private void SetOffer(PlayerSkillUpgrades newOwner, int[] types, int pending)
    {
        if (owner != newOwner)
        {
            // Cambio de dueño (reaparición del jugador): se suelta todo lo del anterior.
            if (showing)
                Release();

            owner = newOwner;
            player = owner.GetComponent<Player>();
            playerInput = owner.GetComponent<PlayerInputController>();
            cameraBinder = owner.GetComponent<PlayerCameraBinder>();
        }

        offerCount = Mathf.Min(types.Length, cards.Length);

        SkillUpgradeDatabase db = SkillUpgradeDatabase.Current;
        bool numberKeys = db.AllowNumberKeys;

        for (int i = 0; i < cards.Length; i++)
        {
            Card card = cards[i];
            bool used = i < offerCount;

            card.root.SetActive(used);

            if (!used)
                continue;

            var type = (SkillUpgradeType)types[i];
            string shortcut = numberKeys ? "   [" + (i + 1) + "]" : string.Empty;

            if (db.TryGet(type, out SkillUpgradeDatabase.Entry entry))
            {
                int next = owner.GetStacks(type) + 1;

                card.key.text = (entry.skillLabel ?? string.Empty).ToUpperInvariant() + shortcut;
                card.title.text = entry.displayName;
                card.description.text = entry.GetDescription();
                card.level.text = "Mejora " + Mathf.Min(next, entry.maxStacks) + "/" + entry.maxStacks;
            }
            else
            {
                card.key.text = shortcut.Trim();
                card.title.text = type.ToString();
                card.description.text = string.Empty;
                card.level.text = string.Empty;
            }

            SetSelected(card, false);
        }

        pendingText.text = pending > 1 ? "Mejoras pendientes: " + pending : string.Empty;
        pendingText.gameObject.SetActive(pending > 1);

        hintText.text = numberKeys
            ? "Haz click en una mejora  (o pulsa 1, 2 o 3)"
            : "Haz click en una mejora";

        hasOffer = true;
        lockUntil = Time.unscaledTime + InputGraceSeconds;

        // Si no se puede mostrar ahora (inventario abierto, jugador muerto), Update() lo hará cuando se libere.
        if (!showing && CanPresent())
            Present();
    }

    private void Close()
    {
        hasOffer = false;

        if (showing)
            Release();

        owner = null;
        player = null;
        playerInput = null;
        cameraBinder = null;
        offerCount = 0;
    }

    // =========================================================
    // MOSTRAR / OCULTAR (bloqueo del jugador y pausa)
    // =========================================================

    /// <summary>Se puede mostrar si ninguna otra ventana tiene bloqueado al jugador y está vivo.</summary>
    private bool CanPresent()
    {
        return hasOffer &&
               owner != null &&
               player != null &&
               playerInput != null &&
               !playerInput.IsInputBlocked &&
               !player.IsDead();
    }

    private void Present()
    {
        showing = true;

        dimRoot.SetActive(true);
        panelRoot.SetActive(true);
        group.alpha = 0f;

        AudioManager.PlayUI(UISound.PanelOpen);

        ApplyHold();
    }

    /// <summary>Mantiene al jugador quieto con el mouse libre. Se repite cada frame mientras el cartel está abierto.</summary>
    private void ApplyHold()
    {
        player.SetInputBlocked(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        cameraBinder?.SetLookEnabled(false);

        SetPaused(ShouldPauseTime());
    }

    private void Release()
    {
        showing = false;

        if (dimRoot != null) dimRoot.SetActive(false);
        if (panelRoot != null) panelRoot.SetActive(false);

        SetPaused(false);

        if (player != null)
            player.SetInputBlocked(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        cameraBinder?.SetLookEnabled(true);
    }

    /// <summary>Solo se pausa si el host está jugando solo: con más gente, la partida de los demás sigue.</summary>
    private static bool ShouldPauseTime()
    {
        if (!SkillUpgradeDatabase.Current.PauseGameWhenSolo)
            return false;

        NetworkManager nm = NetworkManager.Singleton;

        return nm != null && nm.IsListening && nm.IsServer && nm.ConnectedClientsIds.Count <= 1;
    }

    private void SetPaused(bool pause)
    {
        if (pause == timePaused)
            return;

        if (pause)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        }

        timePaused = pause;
    }

    private void OnDisable() => SetPaused(false);

    private void OnDestroy()
    {
        // Pase lo que pase (cambio de escena, cierre del juego), nunca se deja la partida pausada.
        SetPaused(false);

        if (instance == this)
            instance = null;
    }

    // =========================================================
    // ELECCIÓN
    // =========================================================

    private void Update()
    {
        if (!hasOffer)
            return;

        // El dueño dejó de existir (desconexión): se cierra todo.
        if (owner == null)
        {
            Close();
            return;
        }

        if (!showing)
        {
            if (CanPresent())
                Present();

            return;
        }

        // Murió con el cartel abierto: se oculta y vuelve a salir cuando reaparezca.
        if (player == null || player.IsDead())
        {
            Release();
            return;
        }

        ApplyHold();

        // Aparición suave.
        if (group.alpha < 1f)
            group.alpha = Mathf.MoveTowards(group.alpha, 1f, Time.unscaledDeltaTime * 6f);

        if (!SkillUpgradeDatabase.Current.AllowNumberKeys ||
            Time.unscaledTime < lockUntil ||
            Keyboard.current == null ||
            IsTypingInInputField())
            return;

        for (int i = 0; i < offerCount; i++)
        {
            if (Keyboard.current[ChoiceKeys[i]].wasPressedThisFrame)
            {
                Choose(i);
                break;
            }
        }
    }

    private void Choose(int slot)
    {
        if (!showing || owner == null || slot < 0 || slot >= offerCount || Time.unscaledTime < lockUntil)
            return;

        // Se espera la respuesta del servidor (siguiente oferta o cierre). El cartel no se oculta solo,
        // así, si algo falla, el jugador no se queda sin poder elegir.
        lockUntil = Time.unscaledTime + ServerWaitSeconds;

        for (int i = 0; i < offerCount; i++)
            SetSelected(cards[i], i == slot);

        AudioManager.PlayUI(UISound.Confirm);

        owner.ChooseUpgrade(slot);
    }

    private static void SetSelected(Card card, bool selected)
    {
        if (card.outline == null)
            return;

        Color gold = UITheme.Current != null ? UITheme.Current.gold : new Color(0.85f, 0.69f, 0.4f, 1f);

        card.outline.effectColor = selected ? gold : BorderColor;

        float thickness = UITheme.Current != null ? UITheme.Current.borderThickness : 2f;
        float distance = selected ? thickness * 2f : thickness;

        card.outline.effectDistance = new Vector2(distance, -distance);
    }

    private static bool IsTypingInInputField()
    {
        EventSystem es = EventSystem.current;

        return es != null &&
               es.currentSelectedGameObject != null &&
               es.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
    }

    // =========================================================
    // HELPERS DE UI
    // =========================================================

    private static GameObject NewObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StyleImage(Image image, Color color, Sprite sprite)
    {
        image.color = color;

        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }
    }

    private static Outline AddBorder(GameObject go)
    {
        Outline outline = go.AddComponent<Outline>();
        outline.useGraphicAlpha = false;

        UITheme theme = UITheme.Current;

        outline.effectColor = BorderColor;
        float thickness = theme != null ? theme.borderThickness : 2f;
        outline.effectDistance = new Vector2(thickness, -thickness);

        return outline;
    }

    private static TMP_Text NewText(
        string name,
        Transform parent,
        UITheme.TextStyle style,
        float size,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject go = NewObject(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();

        UITheme theme = UITheme.Current;

        if (theme != null)
        {
            UITheme.TextStyleData data = theme.GetTextStyle(style);

            if (data.font != null)
                text.font = data.font;

            text.characterSpacing = data.characterSpacing;
        }

        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;   // el click lo recibe la tarjeta, no el texto

        return text;
    }
}
