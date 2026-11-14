using System.Collections.Generic;

/// <summary>
/// Contrato que comparten todos los items que se pueden equipar.
/// EquipmentData y WeaponData lo implementan por separado.
/// El EquipmentController trabaja contra esta interfaz, nunca contra las clases concretas.
/// </summary>
public interface IEquippable
{
    EquipmentSlot Slot { get; }
    List<StatModifier> Modifiers { get; }

    /// <summary>Clases que pueden equiparlo. Vacío = cualquier clase.</summary>
    IReadOnlyList<PlayerClassData> AllowedClasses { get; }
}

/// <summary>Restricción de clase para items equipables.</summary>
public static class EquippableExtensions
{
    /// <summary>
    /// True si la clase puede equipar el item. Sin restricción (lista vacía) o
    /// sin clase asignada al jugador, se permite (no rompe escenas de prueba).
    /// </summary>
    public static bool CanBeUsedBy(this IEquippable item, PlayerClassData playerClass)
    {
        var allowed = item.AllowedClasses;
        if (allowed == null || allowed.Count == 0) return true;
        if (playerClass == null) return true;

        for (int i = 0; i < allowed.Count; i++)
            if (allowed[i] == playerClass) return true;

        return false;
    }

    /// <summary>Nombres de las clases permitidas ("Mago", "Guerrero, Cazador"). "" si no hay restricción.</summary>
    public static string AllowedClassesLabel(this IEquippable item)
    {
        var allowed = item.AllowedClasses;
        if (allowed == null || allowed.Count == 0) return "";

        var names = new List<string>();
        foreach (var c in allowed)
            if (c != null) names.Add(c.DisplayName);

        return string.Join(", ", names);
    }
}
