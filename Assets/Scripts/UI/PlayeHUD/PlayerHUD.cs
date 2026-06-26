using UnityEngine;
using TMPro;
using System.Collections;

public class PlayerHUD : MonoBehaviour
{
    [Header("Bars")]
    [SerializeField] private ResourceBar healthBar;
    [SerializeField] private ResourceBar manaBar;
    [SerializeField] private ResourceBar xpBar;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI levelText;

    private CharacterStatsSyncController sync;
    private bool initialized;

    // =========================
    // INIT
    // =========================

    public void Initialize(CharacterStatsSyncController controller)
    {
        if (initialized) return;
        initialized = true;

        sync = controller;

        // FIX BUG A+B: la versión original llamaba RefreshAll() aquí directamente,
        // cuando todos los NetworkVariables todavía eran 0 → HUD vacío para TODAS
        // las clases. WaitForNetworkSync() existía pero nunca se iniciaba (código muerto).
        StartCoroutine(WaitForNetworkSync());

        Debug.Log("[HUD] Initialize — esperando sync de red...");
    }

    // =========================
    // WAIT SYNC
    // =========================

    private IEnumerator WaitForNetworkSync()
    {
        // FIX BUG C: la versión original también esperaba NetMaxMana.Value > 0,
        // lo que bloqueaba a warriors sin maná. Solo esperamos salud.
        // Timeout de seguridad por si CharacterData.MaxHealth es 0 o no hay host activo.
        float timeout = 10f;
        float elapsed = 0f;

        while (sync.NetMaxHealth.Value <= 0 && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (sync.NetMaxHealth.Value <= 0)
            Debug.LogWarning("[HUD] NetMaxHealth sigue en 0 tras 10s. " +
                             "Verificá que CharacterData.MaxHealth > 0 y que haya un host activo.");

        RefreshAll();
        Subscribe();

        Debug.Log("[HUD] Inicializado correctamente");
    }

    // =========================
    // SUBSCRIBE
    // =========================

    private void Subscribe()
    {
        sync.NetHealth.OnValueChanged += OnHealthChanged;
        sync.NetMaxHealth.OnValueChanged += OnMaxHealthChanged;
        sync.NetMana.OnValueChanged += OnManaChanged;
        sync.NetMaxMana.OnValueChanged += OnMaxManaChanged;   // FIX: faltaba este evento.
        sync.NetXP.OnValueChanged += OnXPChanged;        // Si MaxMana llegaba un frame
        sync.NetXPRequired.OnValueChanged += OnXPRequiredChanged;// tarde, la barra quedaba
        sync.NetLevel.OnValueChanged += OnLevelChanged;     // oculta para siempre.
    }

    // =========================
    // REFRESH
    // =========================

    private void RefreshManaVisibility()
    {
        bool hasMana = sync.NetMaxMana.Value > 0;
        manaBar.gameObject.SetActive(hasMana);
    }

    private void RefreshAll()
    {
        RefreshManaVisibility();

        healthBar.SetInstant(
            sync.NetHealth.Value,
            sync.NetMaxHealth.Value);

        if (sync.NetMaxMana.Value > 0)
        {
            manaBar.SetInstant(
                sync.NetMana.Value,
                sync.NetMaxMana.Value);
        }

        xpBar.SetInstant(
            sync.NetXP.Value,
            sync.NetXPRequired.Value);

        levelText.text = $"Lvl {sync.NetLevel.Value}";
    }

    // =========================
    // EVENTS
    // =========================

    private void OnHealthChanged(float oldVal, float newVal)
    {
        healthBar.SetTarget(newVal, sync.NetMaxHealth.Value);
    }

    private void OnMaxHealthChanged(float oldVal, float newVal)
    {
        healthBar.SetTarget(sync.NetHealth.Value, newVal);
    }

    private void OnManaChanged(float oldVal, float newVal)
    {
        if (sync.NetMaxMana.Value <= 0) return;
        manaBar.SetTarget(newVal, sync.NetMaxMana.Value);
    }

    private void OnMaxManaChanged(float oldVal, float newVal)
    {
        // Re-evaluar visibilidad por si llegó después del RefreshAll inicial
        RefreshManaVisibility();

        if (newVal > 0)
            manaBar.SetInstant(sync.NetMana.Value, newVal);
    }

    private void OnXPChanged(int oldVal, int newVal)
    {
        xpBar.SetTarget(newVal, sync.NetXPRequired.Value);
    }

    private void OnXPRequiredChanged(int oldVal, int newVal)
    {
        xpBar.SetTarget(sync.NetXP.Value, newVal);
    }

    private void OnLevelChanged(int oldVal, int newVal)
    {
        levelText.text = $"Lvl {newVal}";
    }

    // =========================
    // CLEANUP
    // =========================

    private void OnDestroy()
    {
        if (sync == null || !initialized) return;

        sync.NetHealth.OnValueChanged -= OnHealthChanged;
        sync.NetMaxHealth.OnValueChanged -= OnMaxHealthChanged;
        sync.NetMana.OnValueChanged -= OnManaChanged;
        sync.NetMaxMana.OnValueChanged -= OnMaxManaChanged;
        sync.NetXP.OnValueChanged -= OnXPChanged;
        sync.NetXPRequired.OnValueChanged -= OnXPRequiredChanged;
        sync.NetLevel.OnValueChanged -= OnLevelChanged;
    }
}