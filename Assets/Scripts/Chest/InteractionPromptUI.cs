using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Aviso en pantalla tipo "Presioná [E] para ...".
///
/// No hace falta agregarlo a la escena: se crea solo la primera vez que
/// alguien llama a Show(). Pensado para reutilizarse con cualquier objeto
/// interactuable (cofres, palancas, etc.).
///
/// ESTILO Y POSICIÓN: copia fuente, color, tamaño, espaciado, material
/// (borde/sombra), alineación, posición y tamaño del aviso del QuestGiverUI
/// cuando existe en la escena, así ambos avisos se ven idénticos.
/// Si no hay QuestGiverUI (o no se puede leer su posición), usa los colores
/// del UITheme y se ubica en la esquina inferior derecha con margen.
///
/// El "owner" evita que un objeto borre el aviso de otro: solo quien lo
/// mostró puede esconderlo.
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    private static InteractionPromptUI instance;

    private object owner;
    // Margen (px del canvas de referencia) usado cuando no se puede copiar la
    // posición del aviso del QuestGiverUI: esquina inferior derecha.
    private static readonly Vector2 FallbackMargin = new Vector2(60f, 60f);

    private TMP_Text promptText;
    private RectTransform promptRect;
    private bool styleCopied;

    // =========================================================
    // API
    // =========================================================

    public static void Show(object owner, string message)
    {
        EnsureInstance();

        instance.TryCopyQuestGiverStyle();

        instance.owner = owner;
        instance.promptText.text = message;

        if (!instance.promptText.gameObject.activeSelf)
            instance.promptText.gameObject.SetActive(true);
    }

    public static void Hide(object owner)
    {
        if (instance == null || instance.owner != owner)
            return;

        instance.owner = null;

        if (instance.promptText != null)
            instance.promptText.gameObject.SetActive(false);
    }

    // =========================================================
    // CREACIÓN
    // =========================================================

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        var go = new GameObject("InteractionPromptUI");
        instance = go.AddComponent<InteractionPromptUI>();
        instance.Build();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build()
    {
        var canvasGo = new GameObject(
            "InteractionPromptCanvas",
            typeof(Canvas),
            typeof(CanvasScaler));

        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var textGo = new GameObject("PromptText", typeof(RectTransform));
        textGo.transform.SetParent(canvasGo.transform, false);

        promptText = textGo.AddComponent<TextMeshProUGUI>();
        promptText.fontSize = 30f;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = Color.white;
        promptText.raycastTarget = false;

        // Posición por defecto: esquina inferior derecha con margen.
        // Si existe el QuestGiverUI se reemplaza por la suya (ver
        // TryCopyQuestGiverStyle).
        promptRect = textGo.GetComponent<RectTransform>();
        promptRect.anchorMin = promptRect.anchorMax = promptRect.pivot = new Vector2(1f, 0f);
        promptRect.anchoredPosition = new Vector2(-FallbackMargin.x, FallbackMargin.y);
        promptRect.sizeDelta = new Vector2(900f, 60f);
        promptText.alignment = TextAlignmentOptions.BottomRight;

        ApplyThemeFallback();

        promptText.gameObject.SetActive(false);
    }

    // =========================================================
    // ESTILO
    // =========================================================

    /// <summary>
    /// Estilo base con la paleta del UITheme (se usa si no hay QuestGiverUI).
    /// </summary>
    private void ApplyThemeFallback()
    {
        var theme = UITheme.Current;

        if (theme == null)
            return;

        var body = theme.GetTextStyle(UITheme.TextStyle.Body);

        if (body.font != null)
            promptText.font = body.font;

        promptText.color = theme.textPrimary;
        promptText.characterSpacing = body.characterSpacing;
    }

    /// <summary>
    /// Copia el estilo del aviso "Presioná E" del QuestGiverUI. Se reintenta
    /// en cada Show hasta que el QuestGiverUI exista; después ya no.
    /// </summary>
    private void TryCopyQuestGiverStyle()
    {
        if (styleCopied)
            return;

        QuestGiverUI questUI = QuestGiverUI.Instance;

        if (questUI == null)
            return;

        FieldInfo field = typeof(QuestGiverUI).GetField(
            "promptText",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var source = field?.GetValue(questUI) as TMP_Text;

        if (source == null)
            return;

        promptText.font = source.font;
        promptText.fontSharedMaterial = source.fontSharedMaterial;
        promptText.fontSize = source.fontSize;
        promptText.fontStyle = source.fontStyle;
        promptText.color = source.color;
        promptText.characterSpacing = source.characterSpacing;
        promptText.enableAutoSizing = source.enableAutoSizing;
        promptText.alignment = source.alignment;

        TryCopyQuestGiverPosition(source.rectTransform);

        styleCopied = true;
    }

    /// <summary>
    /// Copia posición y tamaño del aviso del QuestGiverUI. Se hace en píxeles
    /// de pantalla, así funciona aunque los canvases tengan escalas distintas.
    /// Si no se puede (canvas no Overlay, rect sin tamaño, fuera de pantalla),
    /// queda la esquina inferior derecha.
    /// </summary>
    private void TryCopyQuestGiverPosition(RectTransform source)
    {
        Canvas sourceCanvas = source.GetComponentInParent<Canvas>();

        if (sourceCanvas == null)
            return;

        sourceCanvas = sourceCanvas.rootCanvas;

        // En Overlay, el mundo de la UI coincide con píxeles de pantalla.
        if (sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            return;

        var corners = new Vector3[4];
        source.GetWorldCorners(corners);

        var screenRect = new Rect(
            corners[0].x, corners[0].y,
            corners[2].x - corners[0].x,
            corners[2].y - corners[0].y);

        if (screenRect.width <= 0f || screenRect.height <= 0f)
            return;

        if (!new Rect(0f, 0f, Screen.width, Screen.height).Overlaps(screenRect))
            return;

        // Nuestro canvas también es Overlay: anclar abajo-izquierda y pasar
        // los píxeles de pantalla a unidades del canvas.
        // El factor se calcula a mano: el CanvasScaler recién creado todavía
        // no lo actualizó en el primer frame. Misma fórmula que ScaleWithScreenSize.
        float scale = Mathf.Pow(2f, Mathf.Lerp(
            Mathf.Log(Screen.width / 1920f, 2f),
            Mathf.Log(Screen.height / 1080f, 2f),
            0.5f));
        scale = Mathf.Max(scale, 0.0001f);

        promptRect.anchorMin = promptRect.anchorMax = promptRect.pivot = Vector2.zero;
        promptRect.anchoredPosition = screenRect.position / scale;
        promptRect.sizeDelta = screenRect.size / scale;
    }
}
