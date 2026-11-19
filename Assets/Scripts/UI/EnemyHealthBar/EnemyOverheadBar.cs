using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra de vida flotante de un enemigo, armada por código (no usa prefab).
///  - Muestra nombre + nivel y la barra de vida con una estela que baja más despacio al recibir daño.
///  - Aparece sola: al acercarte (14 m), al dañarlo (se queda 5 s) o si está herido; se desvanece de lejos y al morir.
///  - Se coloca sobre la cabeza real del enemigo (según sus colliders), siempre mira a la cámara
///    y mantiene un tamaño legible a cualquier distancia.
///  - El jefe (DemiGod) usa una barra más grande, visible desde lejos.
/// Solo se crea en clientes (ver EnemyHealthBarSpawner).
/// </summary>
public class EnemyOverheadBar : MonoBehaviour
{
    // ---- Ajustes ----
    private const float NormalWidth = 130f;
    private const float BossWidth = 260f;
    private const float BarHeight = 11f;
    private const float NameHeight = 18f;
    private const float PixelsToMeters = 0.01f;

    private const float ShowNearDistance = 14f;
    private const float ShowHurtDistance = 30f;
    private const float BossDistance = 80f;
    private const float HurtLinger = 5f;

    private CharacterStatsSyncController sync;
    private Transform target;
    private float height = 2.2f;
    private bool isBoss;
    private string enemyName;

    private Camera cam;
    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform fillRect;
    private RectTransform chipRect;
    private TextMeshProUGUI label;

    private float shownFill = 1f;
    private float chipFill = 1f;
    private float alpha;
    private float hurtTimer;

    // =========================================================
    // CREACIÓN
    // =========================================================

    public static EnemyOverheadBar Create(CharacterStatsSyncController sync, Transform target)
    {
        var go = new GameObject("EnemyOverheadBar");
        var bar = go.AddComponent<EnemyOverheadBar>();
        bar.Init(sync, target);
        return bar;
    }

    private void Init(CharacterStatsSyncController statsSync, Transform targetTransform)
    {
        sync = statsSync;
        target = targetTransform;

        isBoss = target.GetComponent<DemiGodAIController>() != null;

        Enemy enemy = target.GetComponent<Enemy>();
        enemyName = enemy != null && enemy.EnemyType != null
            ? enemy.EnemyType.DisplayName
            : target.name.Replace("(Clone)", "").Trim();

        height = MeasureHeight(target);

        Build();

        shownFill = chipFill = CurrentFraction();
        ApplyFill();
        RefreshLabel();

        sync.NetHealth.OnValueChanged += OnHealthChanged;
        sync.NetLevel.OnValueChanged += OnLevelChanged;
    }

    private static float MeasureHeight(Transform t)
    {
        bool found = false;
        Bounds bounds = new Bounds(t.position, Vector3.zero);

        foreach (Collider c in t.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger || !c.enabled) continue;

            if (!found) { bounds = c.bounds; found = true; }
            else bounds.Encapsulate(c.bounds);
        }

        if (!found) return 2.2f;

        return Mathf.Clamp(bounds.max.y - t.position.y + 0.3f, 1f, 8f);
    }

    private void Build()
    {
        float width = isBoss ? BossWidth : NormalWidth;
        float totalHeight = NameHeight + BarHeight + 4f;

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        RectTransform root = (RectTransform)transform;
        root.sizeDelta = new Vector2(width, totalHeight);
        root.pivot = new Vector2(0.5f, 0f);

        UITheme theme = UITheme.Current;

        Color frameColor = theme != null ? theme.bronze : new Color(0.66f, 0.47f, 0.23f, 1f);
        Color backColor = theme != null ? theme.stoneDark : new Color(0.106f, 0.094f, 0.086f, 1f);
        Color healthColor = theme != null ? theme.health : new Color(0.7f, 0.15f, 0.18f, 1f);
        Color chipColor = Color.Lerp(healthColor, Color.white, 0.55f);

        // --- Nombre y nivel ---
        var nameGo = new GameObject("Name", typeof(RectTransform));
        nameGo.transform.SetParent(transform, false);

        label = nameGo.AddComponent<TextMeshProUGUI>();
        if (theme != null && theme.body.font != null) label.font = theme.body.font;
        label.fontSize = isBoss ? 17f : 13f;
        label.alignment = TextAlignmentOptions.Bottom;
        label.color = theme != null ? theme.textPrimary : new Color(0.91f, 0.86f, 0.75f, 1f);
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;

        RectTransform nameRect = label.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 0f);
        nameRect.pivot = new Vector2(0.5f, 0f);
        nameRect.anchoredPosition = new Vector2(0f, BarHeight + 4f);
        nameRect.sizeDelta = new Vector2(0f, NameHeight);

        // --- Marco ---
        Image frame = NewImage("Frame", transform, frameColor);
        RectTransform frameRect = frame.rectTransform;
        frameRect.anchorMin = new Vector2(0f, 0f);
        frameRect.anchorMax = new Vector2(1f, 0f);
        frameRect.pivot = new Vector2(0.5f, 0f);
        frameRect.anchoredPosition = Vector2.zero;
        frameRect.sizeDelta = new Vector2(0f, BarHeight);

        // --- Fondo (dentro del marco) ---
        Image back = NewImage("Back", frameRect, backColor);
        RectTransform backRect = back.rectTransform;
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.one;
        backRect.offsetMin = new Vector2(1.5f, 1.5f);
        backRect.offsetMax = new Vector2(-1.5f, -1.5f);

        // --- Estela y vida ---
        chipRect = NewImage("Chip", backRect, chipColor).rectTransform;
        StretchLeft(chipRect);

        fillRect = NewImage("Fill", backRect, healthColor).rectTransform;
        StretchLeft(fillRect);
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // Ancla a la izquierda: la barra se achica ajustando anchorMax.x (sin depender de Image.fillOrigin).
    private static void StretchLeft(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // =========================================================
    // EVENTOS
    // =========================================================

    private void OnHealthChanged(float previous, float current)
    {
        if (current < previous)
            hurtTimer = HurtLinger;
    }

    private void OnLevelChanged(int previous, int current) => RefreshLabel();

    private void RefreshLabel()
    {
        if (label == null) return;

        int level = sync != null ? sync.NetLevel.Value : 0;

        UITheme theme = UITheme.Current;
        Color levelColor = theme != null ? theme.textAccent : new Color(0.85f, 0.69f, 0.4f, 1f);
        string hex = ColorUtility.ToHtmlStringRGB(levelColor);

        label.text = level > 0
            ? enemyName + "  <color=#" + hex + ">Nv. " + level + "</color>"
            : enemyName;
    }

    private void OnDestroy()
    {
        if (sync == null) return;

        sync.NetHealth.OnValueChanged -= OnHealthChanged;
        sync.NetLevel.OnValueChanged -= OnLevelChanged;
    }

    // =========================================================
    // LOOP
    // =========================================================

    private float CurrentFraction()
    {
        float max = sync.NetMaxHealth.Value;
        return max <= 0f ? 0f : Mathf.Clamp01(sync.NetHealth.Value / max);
    }

    private void LateUpdate()
    {
        // El enemigo desapareció (despawn / destruido): la barra no tiene sentido.
        if (target == null || sync == null)
        {
            Destroy(gameObject);
            return;
        }

        if (cam == null) cam = Camera.main;

        // --- Valores ---
        float dt = Time.unscaledDeltaTime;
        float targetFill = CurrentFraction();

        shownFill = Mathf.MoveTowards(shownFill, targetFill, dt * 2.5f);
        if (chipFill < shownFill) chipFill = shownFill;
        else chipFill = Mathf.MoveTowards(chipFill, shownFill, dt * 0.35f);

        ApplyFill();

        if (hurtTimer > 0f) hurtTimer -= dt;

        // --- Posición, orientación y tamaño ---
        transform.position = target.position + Vector3.up * height;

        float distance = 0f;

        if (cam != null)
        {
            transform.rotation = cam.transform.rotation;
            distance = Vector3.Distance(cam.transform.position, transform.position);
        }

        // Tamaño aparente casi constante: crece con la distancia, con límites.
        float scale = PixelsToMeters * Mathf.Clamp(distance / 9f, 0.8f, 3f) * (isBoss ? 1.2f : 1f);
        transform.localScale = Vector3.one * scale;

        // --- Visibilidad ---
        float health = sync.NetHealth.Value;
        float max = sync.NetMaxHealth.Value;

        bool dead = health <= 0f;
        bool hurt = max > 0f && health < max - 0.01f;

        bool want;
        if (dead || cam == null) want = false;
        else if (isBoss) want = distance <= BossDistance;
        else want = distance <= ShowHurtDistance && (hurt || hurtTimer > 0f || distance <= ShowNearDistance);

        alpha = Mathf.MoveTowards(alpha, want ? 1f : 0f, dt * (dead ? 8f : 4f));
        group.alpha = alpha;
        canvas.enabled = alpha > 0.01f;
    }

    private void ApplyFill()
    {
        SetWidth(fillRect, shownFill);
        SetWidth(chipRect, chipFill);
    }

    private static void SetWidth(RectTransform rt, float fraction)
    {
        Vector2 max = rt.anchorMax;
        max.x = Mathf.Clamp01(fraction);
        rt.anchorMax = max;
    }
}
