using System.Text;

/// <summary>
/// Arma los textos de objetivos y recompensas de una quest para mostrarlos en UI.
/// </summary>
public static class QuestTextUtil
{
    public static string Objectives(QuestData quest)
    {
        if (quest == null || quest.objectives == null)
            return string.Empty;

        var sb = new StringBuilder();

        foreach (var objective in quest.objectives)
        {
            if (objective == null)
                continue;

            string text = string.IsNullOrWhiteSpace(objective.description)
                ? "Objetivo"
                : objective.description;

            sb.AppendLine($"- {text} (x{objective.requiredAmount})");
        }

        return sb.ToString().TrimEnd();
    }

    public static string Rewards(QuestData quest)
    {
        if (quest == null || quest.rewards == null || quest.rewards.Length == 0)
            return "Recompensa: ninguna";

        var parts = new System.Collections.Generic.List<string>();

        foreach (var reward in quest.rewards)
        {
            if (reward == null)
                continue;

            switch (reward.rewardType)
            {
                case QuestRewardType.Experience:
                    parts.Add($"{reward.amount} XP");
                    break;

                case QuestRewardType.Gold:
                    parts.Add($"{reward.amount} de oro");
                    break;

                case QuestRewardType.Item:
                    string itemName = reward.item != null ? reward.item.name : "Objeto";
                    parts.Add($"{itemName} x{reward.amount}");
                    break;
            }
        }

        return parts.Count == 0
            ? "Recompensa: ninguna"
            : "Recompensa: " + string.Join(", ", parts);
    }
}
