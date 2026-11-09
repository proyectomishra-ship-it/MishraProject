using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Brujula horizontal estilo Skyrim, hecha solo con texto (sin arte):
/// N, NE, E, SE, S, SO, O, NO y marcas "|" cada 15 grados que se desplazan
/// segun hacia donde mira la camara del jugador local.
///
/// Montaje: este script va en un objeto con RectMask2D (el "viewport").
/// Dentro va un TMP_Text que sirve de plantilla; el script lo clona para
/// cada letra y marca. El Norte es el eje +Z del mundo (ver Norte Offset).
/// </summary>
public class CompassBar : MonoBehaviour
{
    private static readonly string[] Directions = { "N", "NE", "E", "SE", "S", "SO", "O", "NO" };

    [Header("Referencias")]
    [Tooltip("Objeto con RectMask2D. Puede ser este mismo.")]
    [SerializeField] private RectTransform viewport;
    [Tooltip("Texto TMP hijo del viewport. Se usa como molde y se desactiva.")]
    [SerializeField] private TMP_Text labelTemplate;

    [Header("Ajustes")]
    [Tooltip("Pixeles que se mueve el texto por cada grado de giro. Menos = brujula mas amplia.")]
    [SerializeField] private float pixelsPerDegree = 3f;
    [Tooltip("Pixeles en los bordes donde el texto se desvanece.")]
    [SerializeField] private float edgeFade = 120f;
    [Tooltip("Grados para rotar el Norte si el mapa no esta orientado a +Z.")]
    [SerializeField] private float northOffset = 0f;
    [SerializeField] private bool showTicks = true;

    private class Item
    {
        public RectTransform rect;
        public TMP_Text text;
        public float angle;
        public float baseAlpha;
    }

    private readonly List<Item> items = new List<Item>();
    private Camera cam;

    private void Awake()
    {
        if (viewport == null) viewport = transform as RectTransform;
        Build();
    }

    private void Build()
    {
        if (labelTemplate == null || viewport == null)
        {
            Debug.LogWarning("[Brujula] Falta asignar Viewport o Label Template.", this);
            return;
        }

        var theme = UITheme.Current;

        Color north    = theme != null ? theme.accent      : labelTemplate.color;
        Color cardinal = theme != null ? theme.textAccent  : labelTemplate.color;
        Color minor    = theme != null ? theme.textMuted   : labelTemplate.color;
        Color tick     = minor;
        tick.a *= 0.6f;

        labelTemplate.gameObject.SetActive(false);

        for (int i = 0; i < Directions.Length; i++)
        {
            bool isCardinal = i % 2 == 0;
            Color color = i == 0 ? north : (isCardinal ? cardinal : minor);
            AddItem(Directions[i], i * 45f, isCardinal ? 1f : 0.7f, color);
        }

        if (showTicks)
        {
            for (int a = 0; a < 360; a += 15)
            {
                if (a % 45 == 0) continue;     // ahi ya hay una letra
                AddItem("|", a, 0.5f, tick);
            }
        }
    }

    private void AddItem(string text, float angle, float sizeScale, Color color)
    {
        var label = Instantiate(labelTemplate, viewport);
        label.gameObject.SetActive(true);      // ThemedText (si existe) aplica su estilo aqui

        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.fontSize *= sizeScale;
        label.color = color;

        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        items.Add(new Item
        {
            rect = rect,
            text = label,
            angle = angle,
            baseAlpha = color.a
        });
    }

    private void LateUpdate()
    {
        if (items.Count == 0) return;

        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        float heading = GetHeading();
        float half = viewport.rect.width * 0.5f;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];

            float delta = Mathf.DeltaAngle(heading, item.angle);
            float x = delta * pixelsPerDegree;

            item.rect.anchoredPosition = new Vector2(x, item.rect.anchoredPosition.y);

            float edge = edgeFade <= 0f
                ? 1f
                : Mathf.Clamp01((half - Mathf.Abs(x)) / edgeFade);

            item.text.alpha = item.baseAlpha * edge;
        }
    }

    /// <summary>Rumbo de la camara en grados (0 = Norte, 90 = Este).</summary>
    private float GetHeading()
    {
        Vector3 f = cam.transform.forward;
        f.y = 0f;

        float yaw = f.sqrMagnitude < 0.0001f
            ? cam.transform.eulerAngles.y
            : Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;

        return Mathf.Repeat(yaw + northOffset, 360f);
    }
}
