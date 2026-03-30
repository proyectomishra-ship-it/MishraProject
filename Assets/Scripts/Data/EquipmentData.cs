using UnityEngine;

[CreateAssetMenu(fileName = "EquipmentData", menuName = "RPG/Equipment")]
public class EquipmentData : ItemData
{
    [Header("Equipment")]
    [SerializeField] private EquipmentSlot slot;

    [Header("Stat Bonuses")]
    [SerializeField] private float attackBonus;
    [SerializeField] private float defenseBonus;
    [SerializeField] private float maxHealthBonus;
    [SerializeField] private float maxManaBonus;

    public EquipmentSlot Slot => slot;

    public float AttackBonus => attackBonus;
    public float DefenseBonus => defenseBonus;
    public float MaxHealthBonus => maxHealthBonus;
    public float MaxManaBonus => maxManaBonus;
}