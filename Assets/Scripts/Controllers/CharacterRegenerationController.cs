using UnityEngine;
using Unity.Netcode;

public class CharacterRegenerationController : NetworkBehaviour
{
    [Header("Tick Settings")]
    [SerializeField] private float tickRate = 0.25f;

    [Header("Health Regen")]
    [SerializeField] private bool enableHealthRegen = true;

    [SerializeField]
    private float healthRegenPerVitality = 0.20f;

    [SerializeField]
    private float healthCombatDelay = 5f;

    [Header("Mana Regen")]
    [SerializeField] private bool enableManaRegen = true;

    [SerializeField]
    private float manaRegenPerIntelligence = 0.30f;

    [Header("Stamina Regen")]
    [SerializeField] private bool enableStaminaRegen = true;

    [SerializeField]
    private float staminaRegenPerStamina = 0.50f;

    private Character character;
    private CharacterStats stats;
    private ResourceController resources;

    private float tickTimer;

    private void Awake()
    {
        character = GetComponent<Character>();
        resources = GetComponent<ResourceController>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        stats = character.GetStats();

        if (stats == null)
        {
            Debug.LogError(
                $"[Regen] CharacterStats NULL en {gameObject.name}");
        }

        if (resources == null)
        {
            Debug.LogError(
                $"[Regen] ResourceController NULL en {gameObject.name}");
        }
    }

    private void Update()
    {
        if (!IsServer)
            return;

        if (stats == null || resources == null)
            return;

        tickTimer += Time.deltaTime;

        if (tickTimer < tickRate)
            return;

        ProcessRegeneration(tickTimer);

        tickTimer = 0f;
    }

    private void ProcessRegeneration(float deltaTime)
    {
        RegenerateHealth(deltaTime);
        RegenerateMana(deltaTime);
        RegenerateStamina(deltaTime);
    }

    // =====================================
    // HEALTH
    // =====================================

    private void RegenerateHealth(float deltaTime)
    {
        if (!enableHealthRegen)
            return;

        if (stats.CurrentHealth >= stats.MaxHealth.Value)
            return;

        if (Time.time - stats.LastDamageTime < healthCombatDelay)
            return;

        float amount =
            stats.Vitality.Value *
            healthRegenPerVitality *
            deltaTime;

        if (amount > 0f)
        {
            resources.Heal(amount);
        }
    }

    // =====================================
    // MANA
    // =====================================

    private void RegenerateMana(float deltaTime)
    {
        if (!enableManaRegen)
            return;

        if (stats.CurrentMana >= stats.MaxMana.Value)
            return;

        float amount =
            stats.Intelligence.Value *
            manaRegenPerIntelligence *
            deltaTime;

        if (amount > 0f)
        {
            resources.AddMana(amount);
        }
    }

    // =====================================
    // STAMINA
    // =====================================

    private void RegenerateStamina(float deltaTime)
    {
        if (!enableStaminaRegen)
            return;

        if (stats.CurrentStamina >= stats.Stamina.Value)
            return;

        float amount =
            stats.Stamina.Value *
            staminaRegenPerStamina *
            deltaTime;

        if (amount > 0f)
        {
            resources.RecoverStamina(amount);
        }
    }
}