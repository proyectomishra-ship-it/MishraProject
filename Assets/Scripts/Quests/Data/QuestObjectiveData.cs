using UnityEngine;

public abstract class QuestObjectiveData : ScriptableObject
{
    [Header("Objective")]
    public string objectiveID;

    [TextArea(2, 5)]
    public string description;

    [Min(1)]
    public int requiredAmount = 1;
}