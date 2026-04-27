using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PlayerHUD : MonoBehaviour
{
    [Header("Bars")]
    [SerializeField] private Image healthBar;
    [SerializeField] private Image manaBar;
    [SerializeField] private Image xpBar;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI healthText; 
    [SerializeField] private TextMeshProUGUI manaText;   
    [SerializeField] private TextMeshProUGUI xpText;     

    [Header("Animation")]
    [SerializeField] private float smoothSpeed = 8f;

    private CharacterStatsSyncController sync;

    private float targetHealth;
    private float targetMana;
    private float targetXP;

    private float currentHealthFill;
    private float currentManaFill;
    private float currentXPFill;

    private bool initialized = false;

    // =========================
    // INIT
    // =========================

    public void Initialize(CharacterStatsSyncController controller)
    {
        sync = controller;

        sync.NetHealth.OnValueChanged += OnHealthChanged;
        sync.NetMana.OnValueChanged += OnManaChanged;
        sync.NetXP.OnValueChanged += OnXPChanged;
        sync.NetXPRequired.OnValueChanged += OnXPRequiredChanged;
        sync.NetLevel.OnValueChanged += OnLevelChanged;

        StartCoroutine(WaitForInitialSync());
    }

    // =========================
    // ESPERA SYNC
    // =========================

    private IEnumerator WaitForInitialSync()
    {
        yield return new WaitUntil(() =>
            sync != null &&
            sync.NetMaxHealth.Value > 0 &&
            sync.NetMaxMana.Value > 0 &&
            sync.NetXPRequired.Value > 0
        );

        int safeXPRequired = Mathf.Max(sync.NetXPRequired.Value, 1);

        UpdateHealthInstant(sync.NetHealth.Value, sync.NetMaxHealth.Value);
        UpdateManaInstant(sync.NetMana.Value, sync.NetMaxMana.Value);
        UpdateXPInstant(sync.NetXP.Value, safeXPRequired);

        levelText.text = $"Lvl {sync.NetLevel.Value}";

        UpdateAllTexts(); 

        initialized = true;
    }

    // =========================
    // EVENTS
    // =========================

    private void OnHealthChanged(float oldVal, float newVal)
    {
        if (!initialized) return;

        targetHealth = sync.NetMaxHealth.Value > 0
            ? newVal / sync.NetMaxHealth.Value
            : 0f;

        UpdateHealthText(newVal, sync.NetMaxHealth.Value); 
    }

    private void OnManaChanged(float oldVal, float newVal)
    {
        if (!initialized) return;

        targetMana = sync.NetMaxMana.Value > 0
            ? newVal / sync.NetMaxMana.Value
            : 0f;

        UpdateManaText(newVal, sync.NetMaxMana.Value); 
    }

    private void OnXPChanged(int oldVal, int newVal)
    {
        if (!initialized) return;

        targetXP = sync.NetXPRequired.Value > 0
            ? (float)newVal / sync.NetXPRequired.Value
            : 0f;

        UpdateXPText(newVal, sync.NetXPRequired.Value); 
    }

    private void OnXPRequiredChanged(int oldVal, int newVal)
    {
        if (!initialized) return;

        targetXP = newVal > 0
            ? (float)sync.NetXP.Value / newVal
            : 0f;

        UpdateXPText(sync.NetXP.Value, newVal); 
    }

    private void OnLevelChanged(int oldVal, int newVal)
    {
        levelText.text = $"Lvl {newVal}";
    }

    // =========================
    // INSTANT
    // =========================

    private void UpdateHealthInstant(float current, float max)
    {
        currentHealthFill = max > 0 ? current / max : 0f;
        targetHealth = currentHealthFill;
        healthBar.fillAmount = currentHealthFill;
    }

    private void UpdateManaInstant(float current, float max)
    {
        currentManaFill = max > 0 ? current / max : 0f;
        targetMana = currentManaFill;
        manaBar.fillAmount = currentManaFill;
    }

    private void UpdateXPInstant(int current, int required)
    {
        currentXPFill = required > 0 ? (float)current / required : 0f;
        targetXP = currentXPFill;
        xpBar.fillAmount = currentXPFill;
    }

    // =========================
    // TEXT UPDATES 
    // =========================

    private void UpdateAllTexts()
    {
        UpdateHealthText(sync.NetHealth.Value, sync.NetMaxHealth.Value);
        UpdateManaText(sync.NetMana.Value, sync.NetMaxMana.Value);
        UpdateXPText(sync.NetXP.Value, sync.NetXPRequired.Value);
    }

    private void UpdateHealthText(float current, float max)
    {
        healthText.text = $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(max)}";
    }

    private void UpdateManaText(float current, float max)
    {
        manaText.text = $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(max)}";
    }

    private void UpdateXPText(int current, int required)
    {
        xpText.text = $"{current} / {required}";
    }

    // =========================
    // UPDATE
    // =========================

    private void Update()
    {
        if (!initialized) return;

        AnimateBars();
    }

    private void AnimateBars()
    {
        currentHealthFill = Mathf.Lerp(currentHealthFill, targetHealth, Time.deltaTime * smoothSpeed);
        currentManaFill = Mathf.Lerp(currentManaFill, targetMana, Time.deltaTime * smoothSpeed);
        currentXPFill = Mathf.Lerp(currentXPFill, targetXP, Time.deltaTime * smoothSpeed);

        healthBar.fillAmount = currentHealthFill;
        manaBar.fillAmount = currentManaFill;
        xpBar.fillAmount = currentXPFill;
    }

    // =========================
    // CLEANUP
    // =========================

    private void OnDestroy()
    {
        if (sync == null) return;

        sync.NetHealth.OnValueChanged -= OnHealthChanged;
        sync.NetMana.OnValueChanged -= OnManaChanged;
        sync.NetXP.OnValueChanged -= OnXPChanged;
        sync.NetXPRequired.OnValueChanged -= OnXPRequiredChanged;
        sync.NetLevel.OnValueChanged -= OnLevelChanged;
    }
}