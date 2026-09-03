using UnityEngine;
using TMPro;

public class QuestTrackerUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject questTrackerPanel;
    [SerializeField] private TMP_Text questTitleText;
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text questObjectivesText;
    [SerializeField] private TMP_Text questRewardsText;

private NetworkQuestManager questManager;

    private void OnEnable()
    {
        TryInitialize();
    }

    private void OnDisable()
    {
        if (questManager != null)
        {
            questManager.OnQuestDataChanged -= RefreshUI;
        }
    }

    private void Update()
    {
        if (questManager == null)
        {
            TryInitialize();
        }
    }

    private void TryInitialize()
    {
        if (NetworkQuestManager.Instance == null)
        {
            return;
        }

        if (questManager != NetworkQuestManager.Instance)
        {
            if (questManager != null)
            {
                questManager.OnQuestDataChanged -= RefreshUI;
            }

            questManager = NetworkQuestManager.Instance;
            questManager.OnQuestDataChanged += RefreshUI;
        }

        RefreshUI();
    }

    private void RefreshUI()
    {
        if (questManager == null)
        {
            ClearQuest();
            return;
        }

        if (!questManager.TryGetActiveMainQuest(
            out QuestData questData,
            out NetworkQuestState questState))
        {
            ClearQuest();
            return;
        }

        ShowQuest(questData, questState);
    }

    private void ShowQuest(
        QuestData questData,
        NetworkQuestState questState)
    {
        if (questTrackerPanel != null &&
            !questTrackerPanel.activeSelf)
        {
            questTrackerPanel.SetActive(true);
        }

        if (questTitleText != null)
        {
            questTitleText.text = "Misión principal";
        }

        if (questNameText != null)
        {
            questNameText.text = questData.questName;
        }

        if (questObjectivesText != null)
        {
            questObjectivesText.text = BuildObjectiveText(
                questData,
                questState);
        }

        if (questRewardsText != null)
        {
            questRewardsText.text = BuildRewardText(questData);
        }
    }

    private void ClearQuest()
    {
        if (questTitleText != null)
        {
            questTitleText.text = "No hay misiones activas";
        }


if (questNameText != null)
        {
            questNameText.text = string.Empty;
        }

        if (questObjectivesText != null)
        {
            questObjectivesText.text = string.Empty;
        }

        if (questRewardsText != null)
        {
            questRewardsText.text = string.Empty;
        }


}


    private string BuildObjectiveText(
        QuestData questData,
        NetworkQuestState questState)
    {
        if (questData.objectives == null ||
            questData.objectives.Length == 0)
        {
            return string.Empty;
        }

        QuestObjectiveData objective = questData.objectives[0];

        if (objective == null)
        {
            return string.Empty;
        }

        int current = questState.Progress;
        int required = objective.requiredAmount;

        current = Mathf.Clamp(current, 0, required);

        return objective.description + ": " +
               current + "/" + required;
    }

    private string BuildRewardText(QuestData questData)
    {
        if (questData.rewards == null ||
            questData.rewards.Length == 0)
        {
            return "Recompensa: Ninguna";
        }

        string result = "Recompensa: ";

        bool hasReward = false;

        for (int i = 0; i < questData.rewards.Length; i++)
        {
            QuestRewardData reward = questData.rewards[i];

            if (reward == null)
            {
                continue;
            }

            if (hasReward)
            {
                result += ", ";
            }

            switch (reward.rewardType)
            {
                case QuestRewardType.Experience:
                    result += reward.amount + " XP";
                    break;

                case QuestRewardType.Gold:
                    result += reward.amount + " Oro";
                    break;

                case QuestRewardType.Item:
                    if (reward.item != null)
                    {
                        result += reward.item.name +
                                  " x" +
                                  reward.amount;
                    }
                    else
                    {
                        result += "Objeto x" +
                                  reward.amount;
                    }

                    break;
            }

            hasReward = true;
        }

        if (!hasReward)
        {
            return "Recompensa: Ninguna";
        }

        return result;
    }


}
