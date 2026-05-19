using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "RPG/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Core Stats")]
    [SerializeField] private float attack;
    [SerializeField] private float attackRange;
    [SerializeField] private float defense;
    [SerializeField] private float maxHealth;
    [SerializeField] private float maxMana;
    [SerializeField] private float speed;
    [SerializeField] private float agility;
    [SerializeField] private float criticalChance;
    [SerializeField] private float dexterity; //destreza
    [SerializeField] private float intelligence;
    [SerializeField] private float vitality;
    [SerializeField] private float resistance;
    [SerializeField] private float luck;
    

    [Header("Progression")]
    [SerializeField] private int baseLevel;

    [Header("Equipment")]
    [SerializeField] private WeaponType weaponType;

  
    public float Attack => attack;

    public float AttackRange => attackRange;
    public float Defense => defense;
    public float MaxHealth => maxHealth;
    public float MaxMana => maxMana;
    public float Speed => speed;
    public float Agility => agility;
    public float CriticalChance => criticalChance;
    public float Dexterity => dexterity;
    public float Intelligence => intelligence;
    public float Vitality => vitality;
    public float Resistance => resistance;
    public float Luck => luck;

    public int BaseLevel => baseLevel;
    public WeaponType WeaponType => weaponType;
}