using UnityEngine;

[System.Serializable]
public class StatModifier
{
    public StatType stat;

    [Tooltip("Plano: se suma al stat. Porcentual: value = 0.10 equivale a +10 %.")]
    public float value;

    [Tooltip("Si esta activo, 'value' es una fraccion (0.10 = +10 %) aplicada como multiplicador.")]
    public bool percent;
}
