
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gestiona la mira visual del jugador local.
/// Intenta mostrar un prefab UI y, si no es válido,
/// dibuja una mira básica centrada en la pantalla.
/// El apuntado se calcula desde el centro de la cámara.
/// El offset modifica únicamente la posición visual de la mira.
/// </summary>
public class PlayerCrosshair : NetworkBehaviour
{
    [Header("Crosshair Prefab")]
    [SerializeField] private GameObject crosshairPrefab;

    [Header("Visual Offset")]
    [Tooltip("Desplazamiento visual de la mira en píxeles. " +
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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
            return;

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

                // El prefab necesita un Image con un Sprite válido.
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

                    // El offset modifica solamente la posición visual.
                    rectTransform.anchoredPosition = visualOffset;

                    crosshairImage.enabled = true;
                    crosshairImage.raycastTarget = false;

                    Debug.Log(
                        "[PlayerCrosshair] Prefab de mira inicializado.");

                    return;
                }

                Debug.LogWarning(
                    "[PlayerCrosshair] El prefab no contiene un " +
                    "Image con Sprite válido. Se utilizará la mira básica.");

                Destroy(crosshairInstance);
                crosshairInstance = null;
                crosshairImage = null;
            }
            else
            {
                Debug.LogWarning(
                    "[PlayerCrosshair] No se encontró un Canvas. " +
                    "Se utilizará la mira básica.");
            }
        }
        else
        {
            Debug.LogWarning(
                "[PlayerCrosshair] No hay prefab asignado. " +
                "Se utilizará la mira básica.");
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

    private void OnGUI()
    {
        if (!IsSpawned ||
            !IsOwner ||
            !useFallback ||
            fallbackTexture == null)
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
