using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject para armas. Implementa IEquippable directamente,
/// sin heredar de EquipmentData. Solo tiene lo que un arma necesita.
/// ACCIÓN: reemplaza el contenido de Assets/Scripts/Data/WeaponData.cs existente.
/// Crear en: Assets > Create > RPG > Weapon
/// </summary>
[CreateAssetMenu(menuName = "RPG/Weapon")]
public class WeaponData : ItemData, IEquippable
{
    [Header("Slot y Stats")]
    [SerializeField] private List<StatModifier> modifiers = new();

    [Header("Tipo")]
    [SerializeField] private WeaponType weaponType = WeaponType.Sword;

    [Header("Velocidad")]
    [Tooltip("Ataques por segundo. Modifica el holdThreshold del CombatController.")]
    [SerializeField][Range(0.1f, 5f)] private float attackSpeed = 1f;

    [Header("Proyectil — dejar vacío para armas melee")]
    [Tooltip("Prefab con NetworkProjectile. Null = melee.")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float      projectileSpeed = 20f;

    [Header("Multiplicadores")]
    [SerializeField][Range(1f, 5f)] private float heavyMultiplier   = 2f;
    [SerializeField][Range(1f, 5f)] private float specialMultiplier = 1.5f;
    [SerializeField] private float specialManaCost = 20f;

    [Tooltip("Null = usa el mismo projectilePrefab para el especial.")]
    [SerializeField] private GameObject specialProjectilePrefab;

    // ── IEquippable ──────────────────────────────────────────────────────────
    public EquipmentSlot      Slot      => EquipmentSlot.Weapon;
    public List<StatModifier> Modifiers => modifiers;

    // ── Propiedades propias del arma ─────────────────────────────────────────
    public WeaponType  WeaponType        => weaponType;
    public float       AttackSpeed       => attackSpeed;
    public GameObject  ProjectilePrefab  => projectilePrefab;
    public float       ProjectileSpeed   => projectileSpeed;
    public float       HeavyMultiplier   => heavyMultiplier;
    public float       SpecialMultiplier => specialMultiplier;
    public float       SpecialManaCost   => specialManaCost;
    public bool        IsRanged          => projectilePrefab != null;

    public GameObject GetSpecialProjectile() =>
        specialProjectilePrefab != null ? specialProjectilePrefab : projectilePrefab;
}
