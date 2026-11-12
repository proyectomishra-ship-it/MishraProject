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

    [Header("Side Quest (opcional)")]
    [Tooltip("Si se deja vaci­o, las misiones secundarias no se muestran.")]
    [SerializeField] private GameObject sideQuestPanel;
    [SerializeField] private TMP_Text sideQuestNameText;
    [SerializeField] private TMP_Text sideQuestObjectivesText;
    [SerializeField] private TMP_Text sideQuestRewardsText;

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

            // Se libera la referencia para que, al volver a activarse
            // (por ejemplo al cerrar el inventario), OnEnable se vuelva
            // a suscribir al evento.
            questManager = null;
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
            ClearSideQuest();
            return;
        }

        if (questManager.TryGetActiveMainQuest(
            out QuestData questData,
            out NetworkQuestState questState))
        {
            ShowQuest(questData, questState);
        }
        else
        {
            ClearQuest();
        }

        if (questManager.TryGetActiveSideQuest(
            out QuestData sideData,
            out NetworkQuestState sideState))
        {
            ShowSideQuest(sideData, sideState);
        }
        else
        {
            ClearSideQuest();
        }
    }

    private void ShowSideQuest(
        QuestData questData,
        NetworkQuestState questState)
    {
        if (sideQuestPanel != null && !sideQuestPanel.activeSelf)
            sideQuestPanel.SetActive(true);

        if (sideQuestNameText != null)
            sideQuestNameText.text = questData.questName;

        if (sideQuestObjectivesText != null)
            sideQuestObjectivesText.text = BuildObjectiveText(questData, questState);

        if (sideQuestRewardsText != null)
            sideQuestRewardsText.text = BuildRewardText(questData);
    }

    private void ClearSideQuest()
    {
        if (sideQuestPanel != null && sideQuestPanel.activeSelf)
            sideQuestPanel.SetActive(false);

        if (sideQuestNameText != null)
            sideQuestNameText.text = string.Empty;

        if (sideQuestObjectivesText != null)
            sideQuestObjectivesText.text = string.Empty;

        if (sideQuestRewardsText != null)
            sideQuestRewardsText.text = string.Empty;
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