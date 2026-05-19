using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyAttackType
{
    Melee,
    Archer,
    Mage
}

public enum EnemyStrategyType
{
    None,
    Flank,      
    Cover,      
    Backstab    
}

[CreateAssetMenu(fileName = "EnemyData", menuName = "RPG/Enemy Data")]
public class EnemyData : CharacterData
{
    [Header("Combat")]
    [SerializeField] private EnemyAttackType attackType;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float heavyAttackCooldown = 4f;
    [SerializeField] private float specialAttackCooldown = 8f;
    [SerializeField] private bool hasHeavyAttack = false;
    [SerializeField] private bool hasSpecialAttack = false;

    [Header("Experience")]
    [SerializeField] private int baseExperienceReward = 50;
    [SerializeField] private float classMultiplier = 1f;

    [Header("AI")]
    [SerializeField] private float fleeHealthThreshold = 0.25f;
    [SerializeField] private bool canFlee = true;
    [SerializeField]
    private List<EnemyStrategyType> availableStrategies
        = new List<EnemyStrategyType>();

    [Header("Archer / Mage")]
    [SerializeField] private float preferredCombatDistance = 8f;
    [SerializeField] private GameObject projectilePrefab;

    public EnemyAttackType AttackType => attackType;
    public float AttackCooldown => attackCooldown;
    public float HeavyAttackCooldown => heavyAttackCooldown;
    public float SpecialAttackCooldown => specialAttackCooldown;
    public bool HasHeavyAttack => hasHeavyAttack;
    public bool HasSpecialAttack => hasSpecialAttack;
    public int BaseExperienceReward => baseExperienceReward;
    public float ClassMultiplier => classMultiplier;
    public float FleeHealthThreshold => fleeHealthThreshold;
    public bool CanFlee => canFlee;
    public List<EnemyStrategyType> AvailableStrategies => availableStrategies;
    public float PreferredCombatDistance => preferredCombatDistance;
    public GameObject ProjectilePrefab => projectilePrefab;
}