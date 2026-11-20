
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gestiona la mira visual del jugador local.
/// Intenta mostrar un prefab UI y, si no es v�lido,
/// dibuja una mira b�sica centrada en la pantalla.
/// El apuntado se calcula desde el centro de la c�mara.
/// El offset modifica �nicamente la posici�n visual de la mira.
/// </summary>
public class PlayerCrosshair : NetworkBehaviour
{
    [Header("Crosshair Prefab")]
    [SerializeField] private GameObject crosshairPrefab;

    [Header("Visual Offset")]
    [Tooltip("Desplazamiento visual de la mira en p�xeles. " +
             "X positivo mueve a la derecha e Y positivo hacia arriba.")]
    [SerializeField] private Vector2 visualOffset = Vector2.zero;

    [Header("Fallback Crosshair")]
    [SerializeField, Min(1f)] private float lineLength = 7f;
    [SerializeField, Min(1f)] private float lineThickness = 2f;
    [SerializeField, Min(0f)] private float centerGap = 4f;
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField] private bool drawCenterDot = true;

    private GameObject crosshairInstance;
    private Image crosshairImage;
    private Texture2D fallbackTexture;
    private bool useFallback;
    private PlayerInputController inputController;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
            return;

        inputController = GetComponent<PlayerInputController>();

        InitializeCrosshair();
    }

    private void InitializeCrosshair()
    {
        useFallback = false;

        // Intentar crear la mira a partir del prefab.
        if (crosshairPrefab != null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();

            if (canvas != null)
            {
                crosshairInstance = Instantiate(
                    crosshairPrefab,
                    canvas.transform,
                    false);

                crosshairImage =
                    crosshairInstance.GetComponent<Image>();

                // El prefab necesita un Image con un Sprite v�lido.
                if (crosshairImage != null &&
                    crosshairImage.sprite != null)
                {
                    RectTransform rectTransform =
                        crosshairImage.rectTransform;

                    rectTransform.anchorMin =
                        new Vector2(0.5f, 0.5f);

                    rectTransform.anchorMax =
                        new Vector2(0.5f, 0.5f);

                    rectTransform.pivot =
                        new Vector2(0.5f, 0.5f);

                    // El offset modifica solamente la posici�n visual.
                    rectTransform.anchoredPosition = visualOffset;

                    crosshairImage.enabled = true;
                    crosshairImage.raycastTarget = false;

                    Debug.Log(
                        "[PlayerCrosshair] Prefab de mira inicializado.");

                    return;
                }

                Debug.LogWarning(
                    "[PlayerCrosshair] El prefab no contiene un " +
                    "Image con Sprite v�lido. Se utilizar� la mira b�sica.");

                Destroy(crosshairInstance);
                crosshairInstance = null;
                crosshairImage = null;
            }
            else
            {
                Debug.LogWarning(
                    "[PlayerCrosshair] No se encontr� un Canvas. " +
                    "Se utilizar� la mira b�sica.");
            }
        }
        else
        {
            Debug.LogWarning(
                "[PlayerCrosshair] No hay prefab asignado. " +
                "Se utilizar� la mira b�sica.");
        }

        InitializeFallback();
    }

    private void InitializeFallback()
    {
        useFallback = true;

        fallbackTexture = new Texture2D(
            1,
            1,
            TextureFormat.RGBA32,
            false);

        fallbackTexture.SetPixel(0, 0, Color.white);
        fallbackTexture.Apply();
        fallbackTexture.hideFlags = HideFlags.HideAndDontSave;
    }

    /// <summary>
    /// La mira se oculta cuando hay un menu abierto (inventario, pausa, misiones, mejoras...):
    /// en esos casos el input del jugador esta bloqueado y el cursor liberado.
    /// OnGUI se dibuja SIEMPRE por encima de cualquier Canvas, por eso hay que ocultarla a mano.
    /// </summary>
    private bool ShouldHide()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return true;

        return inputController != null && inputController.IsInputBlocked;
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint)
            return;

        if (!IsSpawned ||
            !IsOwner ||
            !useFallback ||
            fallbackTexture == null ||
            ShouldHide())
        {
            return;
        }

        // OnGUI utiliza coordenadas con Y creciendo hacia abajo.
        // Por eso se resta el offset vertical positivo.
        float centerX =
            Screen.width * 0.5f + visualOffset.x;

        float centerY =
            Screen.height * 0.5f - visualOffset.y;

        // Contorno negro para mantener el contraste.
        DrawCrosshair(
            centerX,
            centerY,
            Color.black,
            lineThickness + 2f,
            lineLength + 2f,
            centerGap);

        DrawCrosshair(
            centerX,
            centerY,
            crosshairColor,
            lineThickness,
            lineLength,
            centerGap);

        if (drawCenterDot)
        {
            DrawRect(
                centerX - 1f,
                centerY - 1f,
                2f,
                2f,
                Color.black);

            DrawRect(
                centerX,
                centerY,
                1f,
                1f,
                crosshairColor);
        }
    }

    private void DrawCrosshair(
        float x,
        float y,
        Color color,
        float thickness,
        float length,
        float gap)
    {
        DrawRect(
            x - length - gap,
            y - thickness * 0.5f,
            length,
            thickness,
            color);

        DrawRect(
            x + gap,
            y - thickness * 0.5f,
            length,
            thickness,
            color);

        DrawRect(
            x - thickness * 0.5f,
            y - length - gap,
            thickness,
            length,
            color);

        DrawRect(
            x - thickness * 0.5f,
            y + gap,
            thickness,
            length,
            color);
    }

    private void DrawRect(
        float x,
        float y,
        float width,
        float height,
        Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;

        GUI.DrawTexture(
            new Rect(x, y, width, height),
            fallbackTexture);

        GUI.color = previousColor;
    }

    private void Update()
    {
        // Mira basada en prefab (Canvas): se oculta igual que la basica.
        if (IsOwner && crosshairImage != null)
            crosshairImage.enabled = !ShouldHide();
    }

    public override void OnNetworkDespawn()
    {
        if (crosshairInstance != null)
        {
            Destroy(crosshairInstance);
            crosshairInstance = null;
        }

        crosshairImage = null;
        useFallback = false;

        if (fallbackTexture != null)
        {
            Destroy(fallbackTexture);
            fallbackTexture = null;
        }

        base.OnNetworkDespawn();
    }
}
