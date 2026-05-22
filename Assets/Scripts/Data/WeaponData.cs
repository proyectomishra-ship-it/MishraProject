using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject para armas.
/// Compatible con:
/// - melee
/// - heavy attack
/// - ranged
/// - magic
/// - futuros spells/skills
/// </summary>
[CreateAssetMenu(menuName = "RPG/Weapon")]
public class WeaponData : ItemData, IEquippable
{
    // =========================================================
    // EQUIPMENT
    // =========================================================

    [Header("Slot y Stats")]
    [SerializeField] private List<StatModifier> modifiers = new();

    // =========================================================
    // WEAPON TYPE
    // =========================================================

    [Header("Tipo")]
    [SerializeField] private WeaponType weaponType = WeaponType.Sword;

    // =========================================================
    // ATTACK SPEED
    // =========================================================

    [Header("Velocidad")]
    [Tooltip("Ataques por segundo.")]
    [SerializeField]
    [Range(0.1f, 5f)]
    private float attackSpeed = 1f;

    // =========================================================
    // PROJECTILES
    // =========================================================

    [Header("Proyectiles")]
    [Tooltip("Null = arma melee")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("Velocidad del proyectil")]
    [SerializeField] private float projectileSpeed = 20f;

    [Tooltip("Prefab especial para magia/skill")]
    [SerializeField] private GameObject specialProjectilePrefab;

    // =========================================================
    // DAMAGE MULTIPLIERS
    // =========================================================

    [Header("Multiplicadores")]

    [SerializeField]
    [Range(1f, 5f)]
    private float heavyMultiplier = 2f;

    [SerializeField]
    [Range(1f, 5f)]
    private float specialMultiplier = 1.5f;

    // =========================================================
    // RESOURCE COSTS
    // =========================================================

    [Header("Costos")]

    [Tooltip("Costo de stamina/resistance para heavy attack")]
    [SerializeField]
    private float staminaCost = 25f;

    [Tooltip("Costo de mana para magia/special")]
    [SerializeField]
    private float manaCost = 20f;

    // =========================================================
    // RANGES
    // =========================================================

    [Header("Rangos")]

    [Tooltip("Rango melee")]
    [SerializeField]
    private float meleeRange = 2f;

    [Tooltip("Rango de proyectiles")]
    [SerializeField]
    private float projectileRange = 25f;

    // =========================================================
    // PROPERTIES
    // =========================================================

    public EquipmentSlot Slot => EquipmentSlot.Weapon;

    public List<StatModifier> Modifiers => modifiers;

    public WeaponType WeaponType => weaponType;

    public float AttackSpeed => attackSpeed;

    public GameObject ProjectilePrefab => projectilePrefab;

    public GameObject SpecialProjectilePrefab => specialProjectilePrefab;

    public float ProjectileSpeed => projectileSpeed;

    public float HeavyMultiplier => heavyMultiplier;

    public float SpecialMultiplier => specialMultiplier;

    public float StaminaCost => staminaCost;

    public float ManaCost => manaCost;

    public float MeleeRange => meleeRange;

    public float ProjectileRange => projectileRange;

    public bool IsRanged => projectilePrefab != null;

    public bool IsMagic =>
        weaponType == WeaponType.Staff ||
        weaponType == WeaponType.Grimoire;

    // =========================================================
    // HELPERS
    // =========================================================

    public GameObject GetSpecialProjectile()
    {
        return specialProjectilePrefab != null
            ? specialProjectilePrefab
            : projectilePrefab;
    }
}