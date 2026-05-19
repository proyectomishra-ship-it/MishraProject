using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Datos de arma.
/// </summary>
[CreateAssetMenu(menuName = "RPG/Weapon")]
public class WeaponData : ItemData, IEquippable
{
    [Header("Equipment")]
    [SerializeField]
    private List<StatModifier> modifiers = new();

    [Header("Weapon")]
    [SerializeField]
    private WeaponType weaponType = WeaponType.Sword;

    [SerializeField]
    private WeaponAttackType attackType =
        WeaponAttackType.Melee;

    [Header("Combat")]

    [SerializeField]
    [Range(0.1f, 5f)]
    private float attackSpeed = 1f;

    [SerializeField]
    [Range(1f, 5f)]
    private float heavyMultiplier = 2f;

    [SerializeField]
    [Range(1f, 5f)]
    private float specialMultiplier = 1.5f;

    [Header("Resources")]

    [SerializeField]
    private float staminaCost = 10f;

    [SerializeField]
    private float manaCost = 20f;

    [Header("Projectile")]

    [SerializeField]
    private GameObject projectilePrefab;

    [SerializeField]
    private GameObject specialProjectilePrefab;

    [SerializeField]
    private float projectileSpeed = 20f;

    [Header("Damage")]

    [SerializeField]
    private DamageType damageType =
        DamageType.Physical;

    // =========================
    // IEquippable
    // =========================

    public EquipmentSlot Slot =>
        EquipmentSlot.Weapon;

    public List<StatModifier> Modifiers =>
        modifiers;

    // =========================
    // WEAPON
    // =========================

    public WeaponType WeaponType =>
        weaponType;

    public WeaponAttackType AttackType =>
        attackType;

    public float AttackSpeed =>
        attackSpeed;

    public float HeavyMultiplier =>
        heavyMultiplier;

    public float SpecialMultiplier =>
        specialMultiplier;

    // =========================
    // RESOURCES
    // =========================

    public float StaminaCost =>
        staminaCost;

    public float ManaCost =>
        manaCost;

    // =========================
    // PROJECTILES
    // =========================

    public GameObject ProjectilePrefab =>
        projectilePrefab;

    public GameObject SpecialProjectilePrefab =>
        specialProjectilePrefab;

    public float ProjectileSpeed =>
        projectileSpeed;

    // =========================
    // DAMAGE
    // =========================

    public DamageType DamageType =>
        damageType;
}