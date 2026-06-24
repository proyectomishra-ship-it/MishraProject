using UnityEngine;
using Unity.Netcode;

public class EnemyHealthBarController : MonoBehaviour
{
    [SerializeField] private ResourceBar resourceBar;

    private CharacterStatsSyncController statsSync;

    public void Initialize(CharacterStatsSyncController sync)
    {
        statsSync = sync;

        resourceBar.SetInstant(
            statsSync.NetHealth.Value,
            statsSync.NetMaxHealth.Value);

        statsSync.NetHealth.OnValueChanged += OnHealthChanged;
        statsSync.NetMaxHealth.OnValueChanged += OnMaxHealthChanged;
    }

    private void OnDestroy()
    {
        if (statsSync == null)
            return;

        statsSync.NetHealth.OnValueChanged -= OnHealthChanged;
        statsSync.NetMaxHealth.OnValueChanged -= OnMaxHealthChanged;
    }

    private void OnHealthChanged(float previous, float current)
    {
        UpdateBar();
    }

    private void OnMaxHealthChanged(float previous, float current)
    {
        UpdateBar();
    }

    private void UpdateBar()
    {
        resourceBar.SetTarget(
            statsSync.NetHealth.Value,
            statsSync.NetMaxHealth.Value);
    }
}