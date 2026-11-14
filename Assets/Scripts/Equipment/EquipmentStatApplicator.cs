/// <summary>
/// Aplica y revierte los StatModifiers de un IEquippable sobre un CharacterStats.
/// Los modificadores planos suman al stat; los porcentuales (percent = true)
/// suman al multiplicador (0.10 = +10 %). No sabe nada de red ni de slots.
/// </summary>
public class EquipmentStatApplicator
{
    private readonly CharacterStats stats;

    public EquipmentStatApplicator(CharacterStats stats) => this.stats = stats;

    public void Apply(IEquippable item) => Apply(item, 1f);

    public void Revert(IEquippable item) => Apply(item, -1f);

    private void Apply(IEquippable item, float sign)
    {
        foreach (var mod in item.Modifiers)
        {
            if (mod.percent) stats.AddMultiplier(mod.stat, mod.value * sign);
            else stats.AddBonus(mod.stat, mod.value * sign);
        }
    }
}
