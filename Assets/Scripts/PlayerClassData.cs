using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerClassData", menuName = "RPG/Player Class Data")]
public class PlayerClassData : ScriptableObject
{
    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        public float value;
    }

    [Header("Nombre")]
    [Tooltip("Nombre que ve el jugador (ej: Guerrero, Cazador, Mago). Vacío = usa el nombre del asset.")]
    [SerializeField] private string displayName;

    [Header("Level Scaling")]
    [SerializeField] private List<StatModifier> levelScaling;

    [Header("Multipliers")]
    [SerializeField] private List<StatModifier> multipliers;

    [Header("Equipamiento inicial")]
    [SerializeField] private WeaponData startingWeapon;

    [Header("Habilidad especial (click derecho)")]
    [Tooltip("Asset de la habilidad de esta clase (MageShockwave, WarriorBlade o HunterBarrage). " +
             "Vacío = usa el especial del arma equipada (comportamiento anterior).")]
    [SerializeField] private SpecialAbility specialAbility;

    [Header("Recursos")]
    [Tooltip("Si está activo, los ataques básicos consumen stamina: liviano = costo de stamina del arma, " +
             "pesado = el triple. Desactivar en clases que ya gastan maná (Mage).")]
    [SerializeField] private bool consumesStaminaOnAttack = true;

    public string DisplayName =>
        !string.IsNullOrEmpty(displayName) ? displayName : name.Replace("Data", "").Trim();

    public List<StatModifier> LevelScaling => levelScaling;
    public List<StatModifier> Multipliers => multipliers;
    public WeaponData StartingWeapon => startingWeapon;
    public SpecialAbility SpecialAbility => specialAbility;
    public bool ConsumesStaminaOnAttack => consumesStaminaOnAttack;
}