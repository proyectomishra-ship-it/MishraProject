using UnityEngine;

public class CharacterStats
{
    public int Level { get; protected set; }

    public Stat Attack { get; protected set; }
    public Stat Defense { get; protected set; }
    public Stat Speed { get; protected set; }
    public Stat Agility { get; protected set; }
    public Stat CriticalChance { get; protected set; }
    public Stat Dexterity { get; protected set; }
    public Stat Intelligence { get; protected set; }
    public Stat Vitality { get; protected set; }
    public Stat Luck { get; protected set; }

    public Stat MaxHealth { get; protected set; }
    public float CurrentHealth { get; set; }
   
    

    public Stat MaxMana { get; protected set; }

    public float CurrentMana { get; set; }
    public Stat Resistance { get; protected set; }
    public float CurrentResistance { get; set; }

    public WeaponType WeaponType { get; protected set; }

    public CharacterStats(CharacterData data)
    {
        Level = data.BaseLevel;

        Attack = new Stat(data.Attack);
        Defense = new Stat(data.Defense);
        Speed = new Stat(data.Speed);
        Agility = new Stat(data.Agility);
        CriticalChance = new Stat(data.CriticalChance);
        Dexterity = new Stat(data.Dexterity); //destreza
        Intelligence = new Stat(data.Intelligence);
        Vitality = new Stat(data.Vitality);
        Luck = new Stat(data.Luck);

        MaxHealth = new Stat(data.MaxHealth);
        MaxMana = new Stat(data.MaxMana);
        Resistance = new Stat(data.Resistance);

        CurrentHealth = MaxHealth.Value;
        CurrentMana = MaxMana.Value;
        CurrentResistance = Resistance.Value;

        WeaponType = data.WeaponType;
    }

   
}