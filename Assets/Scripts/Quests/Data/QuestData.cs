using UnityEngine;

[CreateAssetMenu(
    fileName = "New Quest",
    menuName = "RPG/Quests/Quest Data"
)]
public class QuestData : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Unique ID used by the networking system.")]
    public string questID;

    [Header("Basic Information")]
    public string questName;

    [TextArea(3, 8)]
    public string description;

    [Header("Quest Type")]
    public QuestType questType;

    [Header("Objectives")]
    public QuestObjectiveData[] objectives;

    [Header("Rewards")]
    public QuestRewardData[] rewards;

    [Header("Quest Chain")]
    public QuestData nextQuest;
}

public enum QuestType
{
    Main,
    Side
}