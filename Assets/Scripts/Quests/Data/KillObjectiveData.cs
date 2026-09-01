using UnityEngine;

[CreateAssetMenu(
    fileName = "New Kill Objective",
    menuName = "RPG/Quests/Objectives/Kill"
)]
public class KillObjectiveData : QuestObjectiveData
{
    [Header("Target")]
    [Tooltip("Tipo de enemigo que debe ser derrotado.")]
    [SerializeField] private EnemyTypeData enemyType;

    // =========================================================
    // PROPERTIES
    // =========================================================

    public EnemyTypeData EnemyType => enemyType;

    public string EnemyTypeID =>
        enemyType != null
            ? enemyType.EnemyTypeID
            : string.Empty;
}