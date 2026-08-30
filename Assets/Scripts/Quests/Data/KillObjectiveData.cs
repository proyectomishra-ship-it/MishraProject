using UnityEngine;

[CreateAssetMenu(
    fileName = "New Kill Objective",
    menuName = "RPG/Quests/Objectives/Kill"
)]
public class KillObjectiveData : QuestObjectiveData
{
    [Header("Target")]
    public string enemyTypeID;
}