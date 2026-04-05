using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EquipmentData", menuName = "RPG/Equipment")]
public class EquipmentData : ItemData
{
    [Header("Equipment")]
    [SerializeField] private EquipmentSlot slot;

    [Header("Legacy Bonuses (opcional)")]
    [SerializeField] private float attackBonus;
    [SerializeField] private float defenseBonus;
    [SerializeField] private float maxHealthBonus;
    [SerializeField] private float maxManaBonus;

    [Header("Modifiers (NUEVO SISTEMA)")]
    [SerializeField] private List<StatModifier> modifiers;

    public EquipmentSlot Slot => slot;
    public List<StatModifier> Modifiers => modifiers;
}