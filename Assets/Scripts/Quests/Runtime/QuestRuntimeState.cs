using System;

[Serializable]
public class QuestRuntimeState
{
    public string questID;

    public QuestState state;

    public QuestObjectiveRuntime[] objectives;

    public QuestRuntimeState(
        string questID,
        QuestState state,
        int objectiveCount)
    {
        this.questID = questID;
        this.state = state;

        objectives = new QuestObjectiveRuntime[objectiveCount];

        for (int i = 0; i < objectiveCount; i++)
        {
            objectives[i] = new QuestObjectiveRuntime();
        }
    }
}

