using UnityEngine;

[CreateAssetMenu(
    fileName = "New Enemy Type",
    menuName = "RPG/Enemies/Enemy Type"
)]
public class EnemyTypeData : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("ID interno único utilizado por el sistema de misiones.")]
    [SerializeField] private string enemyTypeID;

    [Header("Display")]
    [Tooltip("Nombre mostrado en interfaces y herramientas.")]
    [SerializeField] private string displayName;

    // =========================================================
    // PROPERTIES
    // =========================================================

    public string EnemyTypeID => enemyTypeID;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(displayName)
            ? name
            : displayName;

#if UNITY_EDITOR

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = name;
        }
    }

#endif
}