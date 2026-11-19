using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Indicador en pantalla de la habilidad especial (click derecho): nombre, recarga, nivel de
/// desbloqueo y coste de maná. Se crea solo cuando aparece el jugador local (lo llama
/// PlayerCombatController), igual que SkillNoticeUI: no hay que tocar la escena ni crear prefabs.
///
/// Usa solo elementos de UI de Unity (Image y TextMeshPro) y los colores del UITheme.
/// No usa imagenes propias: el sprite es el cuadrado blanco que trae Unity (Texture2D.whiteTexture).
///
/// Estados:
///   - Bloqueado        : "Nv 8"  (nivel al que se desbloquea)
///   - En recarga       : sombra que se va abriendo + segundos que faltan
///   - Sin mana         : borde azul y "Sin mana" (solo Mage)
///   - Lista            : borde dorado, "Clic der.", y un destello cuando termina la recarga
/// </summary>
public class SpecialCooldownHUD : MonoBehaviour
{
    // ---- Posicion y tamano (cambiar estos numeros para moverlo) ----
    private const float AnchorX = 1f;     // 0 = izquierda, 1 = derecha
    private const float AnchorY = 0f;       // 0 = abajo, 1 = arriba
    private const float OffsetX = -40f;       // pixeles (referencia 1920x1080)
    private const float OffsetY = 150f;
    private const float SlotSize = 84f;
    private const float BorderSize = 3f;
    private const float FlashSeconds = 0.4f;

    private enum State { Locked = 0, Cooldown = 1, NoMana = 2, Ready = 3 }

    private static SpecialCooldownHUD instance;
    private static Sprite whiteSprite;

    private PlayerCombatController combat;
    private CharacterStatsSyncController sync;
    private SpecialAbility shownAbility;

    private GameObject panel;
    private Image frame, background, shade, flash;
    private TextMeshProUGUI centerText, nameText, costText;

    // Paleta (del UITheme; si no hay tema se usan colores de respaldo)
    private Color cStone, cStoneMid, cBronze, cGold, cText, cMuted, cMana;

    private State lastState = (State)(-1);
    private int lastTenths = -1;
    private float lastManaCost = -1f;
    private bool wasOnCooldown;
    private float flashTimer;

    // =========================================================
    // CREAR / QUITAR
    // =========================================================

    /// <summary>Lo llama el jugador local al aparecer.</summary>
    public static void Attach(PlayerCombatController owner)
    {
        if (owner == null)
            return;

        if (instance != null)
            Destroy(instance.gameObject);

        var go = new GameObject("SpecialCooldownHUD");
        instance = go.AddComponent<SpecialCooldownHUD>();
        instance.combat = owner;
        instance.sync = owner.GetComponent<CharacterStatsSyncController>();
        instance.Build();
    }

    public static void Detach(PlayerCombatController owner)
    {
        if (instance != null && instance.combat == owner)
        {
            Destroy(instance.gameObject);
            instance = null;
        }
    }

    private void OnDestroy()
    {
        UITheme.OnThemeChanged -= LoadPalette;

        if (instance == this)
            instance = null;
    }

    // =========================================================
    // CONSTRUCCION DE LA UI
    // =========================================================

    private void Build()
    {
        LoadPalette();
        UITheme.OnThemeChanged += LoadPalette;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;                           // por debajo de los avisos (SkillNoticeUI = 40)

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        panel = new GameObject("Slot", typeof(RectTransform));
        panel.transform.SetParent(transform, false);

        var rt = (RectTransform)panel.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(AnchorX, AnchorY);
        rt.pivot = new Vector2(AnchorX, 0f);
        rt.anchoredPosition = new Vector2(OffsetX, OffsetY);
        rt.sizeDelta = new Vector2(SlotSize, SlotSize);

        frame = MakeImage("Frame", panel.transform);
        Stretch(frame.rectTransform, 0f);

        background = MakeImage("Background", panel.transform);
        Stretch(background.rectTransform, BorderSize);

        // Sombra de recarga: un circulo que se va "abriendo" en sentido horario.
        shade = MakeImage("Shade", panel.transform);
        Stretch(shade.rectTransform, BorderSize);
        shade.type = Image.Type.Filled;
        shade.fillMethod = Image.FillMethod.Radial360;
        shade.fillOrigin = (int)Image.Origin360.Top;
        shade.fillClockwise = false;
        shade.fillAmount = 0f;

        flash = MakeImage("Flash", panel.transform);
        Stretch(flash.rectTransform, BorderSize);
        flash.color = Color.clear;

        centerText = MakeText("Center", panel.transform, 30f, FontStyles.Bold);
        CenterRect(centerText.rectTransform, new Vector2(160f, SlotSize));

        nameText = MakeText("Name", panel.transform, 18f, FontStyles.Normal);
        var nr = nameText.rectTransform;
        nr.anchorMin = nr.anchorMax = new Vector2(0.5f, 1f);
        nr.pivot = new Vector2(0.5f, 0f);
        nr.anchoredPosition = new Vector2(0f, 4f);
        nr.sizeDelta = new Vector2(260f, 24f);

        costText = MakeText("Cost", panel.transform, 16f, FontStyles.Normal);
        var cr = costText.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f);
        cr.pivot = new Vector2(0.5f, 1f);
        cr.anchoredPosition = new Vector2(0f, -4f);
        cr.sizeDelta = new Vector2(200f, 22f);

        panel.SetActive(false);                             // se muestra en Update si la clase tiene especial
    }

    private void LoadPalette()
    {
        UITheme t = UITheme.Current;

        cStone = t != null ? t.stoneDark : Hex("1B1816");
        cStoneMid = t != null ? t.stoneMid : Hex("2E2924");
        cBronze = t != null ? t.bronze : Hex("A8783A");
        cGold = t != null ? t.gold : Hex("D9B165");
        cText = t != null ? t.textPrimary : Hex("E8DCC0");
        cMuted = t != null ? t.textMuted : Hex("9C927C");
        cMana = t != null ? t.mana : Hex("2F5FB5");
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }

    private static Sprite White()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;         // textura blanca incluida en Unity
            whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                        new Vector2(0.5f, 0.5f), 100f);
        }
        return whiteSprite;
    }

    private static Image MakeImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = White();
        img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI MakeText(string name, Transform parent, float size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;

        UITheme theme = UITheme.Current;
        if (theme != null)
        {
            UITheme.TextStyleData s = theme.GetTextStyle(UITheme.TextStyle.Caption);
            if (s.font != null) t.font = s.font;
            t.characterSpacing = s.characterSpacing;
        }

        return t;
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static void CenterRect(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }

    // =========================================================
    // ACTUALIZACION
    // =========================================================

    private static string NameOf(SpecialAbility ability)
    {
        if (ability is MageShockwaveAbility) return "Onda Expansiva";
        if (ability is WarriorBladeAbility) return "Hoja de Energía";
        if (ability is HunterBarrageAbility) return "Lluvia de Flechas";
        return ability.name;
    }

    private void Update()
    {
        if (combat == null || !combat.IsSpawned)
        {
            Destroy(gameObject);
            return;
        }

        // Con el inventario abierto el indicador se oculta, como el resto del HUD.
        if (InventoryUI.IsOpen)
        {
            if (panel.activeSelf) panel.SetActive(false);
            return;
        }

        SpecialAbility ability = combat.ClassSpecial;

        if (ability == null)
        {
            if (panel.activeSelf) panel.SetActive(false);
            return;
        }

        if (!panel.activeSelf) panel.SetActive(true);

        if (ability != shownAbility)
        {
            shownAbility = ability;
            nameText.text = NameOf(ability);
            lastManaCost = -1f;
        }

        bool unlocked = combat.IsSpecialUnlocked;
        float remaining = unlocked ? combat.SpecialCooldownRemaining : 0f;
        float total = combat.SpecialCooldownTotal;
        bool onCooldown = remaining > 0.001f;

        float manaCost = combat.SpecialManaCost;
        bool noMana = unlocked && !onCooldown && manaCost > 0f &&
                      sync != null && sync.NetMana.Value < manaCost;

        State state = !unlocked ? State.Locked
                    : onCooldown ? State.Cooldown
                    : noMana ? State.NoMana
                    : State.Ready;

        // Destello cuando termina la recarga.
        if (wasOnCooldown && !onCooldown && unlocked)
            flashTimer = FlashSeconds;
        wasOnCooldown = onCooldown;

        // Coste de mana debajo del cuadro (solo si la habilidad gasta mana).
        if (!Mathf.Approximately(manaCost, lastManaCost))
        {
            lastManaCost = manaCost;
            costText.text = manaCost > 0f ? manaCost.ToString("0.#") + " maná" : "";
            costText.color = cMana;
        }

        // Colores y textos fijos de cada estado (solo cuando cambia el estado).
        if (state != lastState)
        {
            lastState = state;
            lastTenths = -1;

            switch (state)
            {
                case State.Locked:
                    frame.color = cStoneMid;
                    centerText.fontSize = 22f;
                    centerText.color = cMuted;
                    centerText.text = "Nv " + combat.SpecialUnlockLevel;
                    break;

                case State.Cooldown:
                    frame.color = cBronze;
                    centerText.fontSize = 30f;
                    centerText.color = cText;
                    break;

                case State.NoMana:
                    frame.color = cMana;
                    centerText.fontSize = 16f;
                    centerText.color = cMana;
                    centerText.text = "Sin maná";
                    break;

                default:
                    frame.color = cGold;
                    centerText.fontSize = 16f;
                    centerText.color = cGold;
                    centerText.text = "Clic der.";
                    break;
            }

            background.color = new Color(cStone.r, cStone.g, cStone.b, 0.9f);
        }

        // Sombra y segundos restantes.
        if (state == State.Cooldown)
        {
            shade.color = new Color(0f, 0f, 0f, 0.65f);
            shade.fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 1f;

            int tenths = Mathf.CeilToInt(remaining * 10f);
            if (tenths != lastTenths)
            {
                lastTenths = tenths;
                centerText.text = remaining >= 10f
                    ? Mathf.CeilToInt(remaining).ToString()
                    : (tenths / 10f).ToString("0.0");
            }
        }
        else if (state == State.Locked)
        {
            shade.color = new Color(0f, 0f, 0f, 0.5f);      // cuadro apagado
            shade.fillAmount = 1f;
        }
        else
        {
            shade.fillAmount = 0f;
        }

        // Destello.
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            float a = Mathf.Clamp01(flashTimer / FlashSeconds) * 0.55f;
            flash.color = new Color(cGold.r, cGold.g, cGold.b, a);
        }
        else if (flash.color.a > 0f)
        {
            flash.color = Color.clear;
        }
    }
}
