/// <summary>
/// Aplica y revierte los StatModifiers de un IEquippable sobre un CharacterStats.
/// No sabe nada de red ni de slots.
/// ACCIÓN: archivo nuevo en Assets/Scripts/Equipment/
/// </summary>
public class EquipmentStatApplicator
{
    private readonly CharacterStats stats;

    public EquipmentStatApplicator(CharacterStats stats) => this.stats = stats;

    public void Apply(IEquippable item)
    {
        foreach (var mod in item.Modifiers)
            stats.AddBonus(mod.stat, mod.value);
    }

    public void Revert(IEquippable item)
    {
        foreach (var mod in item.Modifiers)
            stats.AddBonus(mod.stat, -mod.value);
    }
}
