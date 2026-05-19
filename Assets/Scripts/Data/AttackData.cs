using UnityEngine;

public struct AttackData
{
    public Character Attacker;
    public Character Target;

    public float Damage;

    public DamageType DamageType;

    public bool IsCritical;
    public bool IsHeavy;

    public Vector3 HitPoint;
}