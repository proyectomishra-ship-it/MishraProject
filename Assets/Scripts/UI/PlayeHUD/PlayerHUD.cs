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

        Debug.Log("[HUD] Initialize");

        sync = controller;

        RefreshAll();

        sync.NetHealth.OnValueChanged += OnHealthChanged;
        sync.NetMana.OnValueChanged += OnManaChanged;
        sync.NetXP.OnValueChanged += OnXPChanged;
        sync.NetXPRequired.OnValueChanged += OnXPRequiredChanged;
        sync.NetLevel.OnValueChanged += OnLevelChanged;
    }

    // =========================
    // WAIT SYNC
    // =========================

    private IEnumerator WaitForNetworkSync()
    {
        yield return new WaitUntil(() =>
            sync != null &&
            sync.NetMaxHealth.Value > 0 &&
            sync.NetMaxMana.Value > 0
        );

        RefreshAll();

        Subscribe();

        initialized = true;

        Debug.Log("[HUD] Inicializado correctamente");
    }

    // =========================
    // SUBSCRIBE
    // =========================

    private void Subscribe()
    {
        sync.NetHealth.OnValueChanged += OnHealthChanged;
        sync.NetMana.OnValueChanged += OnManaChanged;
        sync.NetXP.OnValueChanged += OnXPChanged;
        sync.NetXPRequired.OnValueChanged += OnXPRequiredChanged;
        sync.NetLevel.OnValueChanged += OnLevelChanged;
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
        healthBar.SetTarget(
            newVal,
            sync.NetMaxHealth.Value
        );
    }

    private void OnManaChanged(float oldVal, float newVal)
    {
        if (sync.NetMaxMana.Value <= 0)
            return;

        manaBar.SetTarget(
            newVal,
            sync.NetMaxMana.Value
        );
    }

    private void OnXPChanged(int oldVal, int newVal)
    {
        xpBar.SetTarget(
            newVal,
            sync.NetXPRequired.Value
        );
    }

    private void OnXPRequiredChanged(int oldVal, int newVal)
    {
        xpBar.SetTarget(
            sync.NetXP.Value,
            newVal
        );
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
        if (sync == null || !initialized)
            return;

        sync.NetHealth.OnValueChanged -= OnHealthChanged;
        sync.NetMana.OnValueChanged -= OnManaChanged;
        sync.NetXP.OnValueChanged -= OnXPChanged;
        sync.NetXPRequired.OnValueChanged -= OnXPRequiredChanged;
        sync.NetLevel.OnValueChanged -= OnLevelChanged;
    }
}