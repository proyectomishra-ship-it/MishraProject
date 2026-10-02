using UnityEngine;

[System.Serializable]
public class Stat
{
    private float baseValue;
    private float bonus;
    private float multiplier = 1f;

    public float Value => (baseValue + bonus) * multiplier;

    public Stat(float baseValue)
    {
        this.baseValue = baseValue;
    }

    public void AddBonus(float value)
    {
        bonus += value;
    }

    public void RemoveBonus(float value)
    {
        bonus -= value;
    }

    public void AddMultiplier(float value)
    {
        multiplier += value;
    }
}