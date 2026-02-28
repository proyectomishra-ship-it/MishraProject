using UnityEngine;

[CreateAssetMenu(fileName = "PlayerClassData", menuName = "RPG/Player Class Data")]
public class PlayerClassData : ScriptableObject
{
    [Header("Level Scaling")]
    [SerializeField] private float attackPerLevel;
    [SerializeField] private float defensePerLevel;
    [SerializeField] private float maxHealthPerLevel;
    [SerializeField] private float maxManaPerLevel;
    [SerializeField] private float dexterityPerLevel;
    [SerializeField] private float intelligencePerLevel;
    [SerializeField] private float vitalityPerLevel;
    [SerializeField] private float luckPerLevel;

    public float AttackPerLevel => attackPerLevel;
    public float DefensePerLevel => defensePerLevel;
    public float MaxHealthPerLevel => maxHealthPerLevel;
    public float MaxManaPerLevel => maxManaPerLevel;
    public float DexterityPerLevel => dexterityPerLevel;
    public float IntelligencePerLevel => intelligencePerLevel;
    public float VitalityPerLevel => vitalityPerLevel;
    public float LuckPerLevel => luckPerLevel;
}