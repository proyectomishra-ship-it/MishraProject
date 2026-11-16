using System;
using UnityEngine;

public class PlayerStats : CharacterStats
{
    // =========================
    // EVENTS 
    // =========================

    public event Action<int, int> OnExperienceChanged;

    // =========================
    // DATA
    // =========================

    private PlayerClassData classData;

    public int Experience { get; private set; }
    public int ExperienceRequired { get; private set; }

    private float experienceGrowthFactor = 1.5f;
    private int baseExperienceRequired = 100;

    // =========================
    // CONSTRUCTOR
    // =========================

    public PlayerStats(CharacterData data, PlayerClassData classData)
        : base(data)
    {
        this.classData = classData;

        ApplyMultipliers();

        // Los multiplicadores de clase pueden subir MaxMana despues de que el
        // constructor base ya dejo CurrentMana en el valor sin multiplicar
        // (HandleDerivedStats solo recorta hacia abajo, nunca sube). Se llena
        // aca para que el jugador arranque con el mana al maximo.
        CurrentMana = MaxMana.Value;

        Experience = 0;
        ExperienceRequired = CalculateRequiredXP(Level);

        RaiseXPEvent();
    }

    // =========================
    // EXPERIENCE SYSTEM
    // =========================

    public void AddExperience(int amount)
    {
        if (amount <= 0) return;

        Experience += amount;

       
        RaiseXPEvent();

        ProcessLevelUps();
    }

    private void ProcessLevelUps()
    {
       
        while (Experience >= ExperienceRequired)
        {
            Experience -= ExperienceRequired;

            LevelUpInternal();
        }
    }

    private void LevelUpInternal()
    {
        int newLevel = Level + 1;

        SetLevel(newLevel);

        ApplyLevelScaling();

        ExperienceRequired = CalculateRequiredXP(Level);

        RaiseXPEvent();
    }

    private int CalculateRequiredXP(int level)
    {
        return Mathf.RoundToInt(
            baseExperienceRequired * Mathf.Pow(experienceGrowthFactor, level - 1)
        );
    }

    private void RaiseXPEvent()
    {
        OnExperienceChanged?.Invoke(Experience, ExperienceRequired);
    }

    // =========================
    // CLASS SYSTEM
    // =========================

    private void ApplyLevelScaling()
    {
        foreach (var mod in classData.LevelScaling)
        {
            AddBonus(mod.stat, mod.value);
        }
    }

    /// <summary>
    /// Multiplicadores de clase. El valor se lee como el factor final sobre el
    /// stat base: 1.2 = x1.2 (+20%), 0.9 = x0.9 (-10%).
    ///
    /// Stat.AddMultiplier es ADITIVO a proposito (lo usan el equipamiento y
    /// las auras, que suman y restan porcentajes). Por eso el factor se pasa
    /// como (valor - 1): 1.2 suma +0.2 al multiplicador y queda en x1.2, no x2.2.
    /// Los porcentajes del equipamiento se siguen sumando encima de este.
    /// </summary>
    private void ApplyMultipliers()
    {
        foreach (var mod in classData.Multipliers)
        {
            // Un factor <= 0 dejaria el stat en cero o negativo. Suele ser un
            // resto de la convencion vieja (ej: -0.05 para "-5%"). Se ignora.
            if (mod.value <= 0f)
            {
                Debug.LogWarning(
                    $"[PlayerStats] Multiplicador de clase invalido para {mod.stat}: " +
                    $"{mod.value}. Debe ser el factor final (ej: 0.95 = -5%). Se ignora."
                );
                continue;
            }

            AddMultiplier(mod.stat, mod.value - 1f);
        }
    }

    // =========================
    //  FORCED SYNC
    // =========================

    public void ForceSync()
    {
  
        RaiseAllEvents();
        RaiseXPEvent();
    }
}