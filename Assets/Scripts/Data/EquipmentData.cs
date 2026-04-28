using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject para equipamiento no-arma: cascos, armaduras, botas, anillos, amuletos.
/// Implementa IEquippable directamente.
/// ACCIÓN: reemplaza Assets/Scripts/Data/EquipmentData.cs existente.
/// Se elimina el campo itemId manual (lo resuelve ItemDatabase) y los legacy bonuses.
/// Crear en: Assets > Create > RPG > Equipment
/// </summary>
[CreateAssetMenu(menuName = "RPG/Equipment")]
public class EquipmentData : ItemData, IEquippable
{
    [Header("Slot")]
    [SerializeField] private EquipmentSlot slot;

    [Header("Stats")]
    [SerializeField] private List<StatModifier> modifiers = new();

    // ── IEquippable ──────────────────────────────────────────────────────────
    public EquipmentSlot      Slot      => slot;
    public List<StatModifier> Modifiers => modifiers;
}
