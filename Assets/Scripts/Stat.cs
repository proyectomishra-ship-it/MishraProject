using System;

[Serializable]
public class Stat
{
    private float baseValue;
    private float bonusValue;
    private float multiplier = 1f;

    public float Value => (baseValue + bonusValue) * multiplier;

    public Stat(float baseValue)
    {
        this.baseValue = baseValue;
    }

    public void SetBase(float value)
    {
        baseValue = value;
    }

    public void AddBonus(float value)
    {
        bonusValue += value;
    }

    public void RemoveBonus(float value)
    {
        bonusValue -= value;
    }

    public void SetMultiplier(float value)
    {
        multiplier = value;
    }
}