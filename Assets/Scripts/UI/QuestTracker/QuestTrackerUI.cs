using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tablero de misiones (principal y secundaria).
///
/// Tiene dos vistas que se alternan con una tecla (por defecto J):
///   - Compacta: t�tulo + objetivo con su progreso.
///   - Completa: adem�s el nombre de la misi�n y la recompensa.
/// La vista elegida se recuerda entre sesiones (PlayerPrefs).
///
/// Los paneles se ajustan solos al contenido (VerticalLayoutGroup +
/// ContentSizeFitter agregados en Awake si no existen) y el panel secundario
/// se coloca justo debajo del principal, sin superponerse.
/// </summary>
public class QuestTrackerUI : MonoBehaviour
{
    private const string ExpandedPrefKey = "Mishra.QuestTracker.Expanded";

    [Header("UI References")]
    [SerializeField] private GameObject questTrackerPanel;
    [SerializeField] private TMP_Text questTitleText;
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text questObjectivesText;
    [SerializeField] private TMP_Text questRewardsText;

    [Header("Side Quest (opcional)")]
    [Tooltip("Si se deja vac�o, las misiones secundarias no se muestran.")]
    [SerializeField] private GameObject sideQuestPanel;
    [SerializeField] private TMP_Text sideQuestNameText;
    [SerializeField] private TMP_Text sideQuestObjectivesText;
    [SerializeField] private TMP_Text sideQuestRewardsText;

    [Header("Vista compacta / completa")]
    [Tooltip("Tecla para alternar entre la vista compacta y la completa.")]
    [SerializeField] private Key toggleKey = Key.J;

    [Tooltip("Vista con la que arranca la primera vez. Despu�s se recuerda la �ltima elegida.")]
    [SerializeField] private bool startExpanded = false;

    [Header("Dise�o")]
    [SerializeField] private float paddingHorizontal = 15f;
    [SerializeField] private float paddingVertical = 12f;
    [SerializeField] private float lineSpacing = 4f;
    [Tooltip("Separaci�n entre el panel principal y el secundario.")]
    [SerializeField] private float panelGap = 8f;

    private NetworkQuestManager questManager;
    private bool expanded;
    private RectTransform mainRect;
    private RectTransform sideRect;

    // Misiones que este tablero mostro como activas. Sirve para detectar cuando
    // una de ellas pasa a Completada y avisar con sonido (solo en este cliente).
    private readonly HashSet<string> trackedQuestIds = new HashSet<string>();
    private readonly List<string> completedBuffer = new List<string>();

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    private void Awake()
    {
        expanded = PlayerPrefs.GetInt(ExpandedPrefKey, startExpanded ? 1 : 0) == 1;

        mainRect = questTrackerPanel != null
            ? questTrackerPanel.transform as RectTransform
            : null;

        sideRect = sideQuestPanel != null
            ? sideQuestPanel.transform as RectTransform
            : null;

        EnsureAutoLayout(mainRect);
        EnsureAutoLayout(sideRect);
    }

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

        if (Keyboard.current != null &&
            Keyboard.current[toggleKey].wasPressedThisFrame &&
            !IsTypingInInputField())
        {
            SetExpanded(!expanded);
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

    // =========================================================
    // VISTA COMPACTA / COMPLETA
    // =========================================================

    private void SetExpanded(bool value)
    {
        expanded = value;

        PlayerPrefs.SetInt(ExpandedPrefKey, expanded ? 1 : 0);

        RefreshUI();
    }

    private static bool IsTypingInInputField()
    {
        EventSystem es = EventSystem.current;

        if (es == null || es.currentSelectedGameObject == null)
        {
            return false;
        }

        return es.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
    }

    private string BuildToggleHint()
    {
        UITheme theme = UITheme.Current;

        string hex = theme != null
            ? ColorUtility.ToHtmlStringRGB(theme.textMuted)
            : "9C927C";

        return "  <size=55%><color=#" + hex + ">[" + toggleKey + "] " +
               (expanded ? "reducir" : "ampliar") +
               "</color></size>";
    }

    // =========================================================
    // LAYOUT
    // =========================================================

    private void EnsureAutoLayout(RectTransform panel)
    {
        if (panel == null)
        {
            return;
        }

        if (panel.GetComponent<VerticalLayoutGroup>() == null)
        {
            VerticalLayoutGroup group =
                panel.gameObject.AddComponent<VerticalLayoutGroup>();

            group.padding = new RectOffset(
                Mathf.RoundToInt(paddingHorizontal),
                Mathf.RoundToInt(paddingHorizontal),
                Mathf.RoundToInt(paddingVertical),
                Mathf.RoundToInt(paddingVertical));

            group.spacing = lineSpacing;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
        }

        if (panel.GetComponent<ContentSizeFitter>() == null)
        {
            ContentSizeFitter fitter =
                panel.gameObject.AddComponent<ContentSizeFitter>();

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    /// <summary>
    /// Recalcula el tama�o de ambos paneles y coloca el secundario justo
    /// debajo del principal (los dos est�n anclados arriba a la izquierda).
    /// </summary>
    private void RefreshLayout()
    {
        if (mainRect != null && mainRect.gameObject.activeInHierarchy)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(mainRect);
        }

        if (sideRect != null && sideRect.gameObject.activeInHierarchy)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(sideRect);
        }

        if (mainRect != null && sideRect != null)
        {
            Vector2 position = sideRect.anchoredPosition;

            position.y = mainRect.anchoredPosition.y
                         - mainRect.rect.height
                         - panelGap;

            sideRect.anchoredPosition = position;
        }
    }

    /// <summary>
    /// Asigna el texto y muestra u oculta la l�nea. Una l�nea oculta
    /// no ocupa espacio en el layout.
    /// </summary>
    private static void SetLine(TMP_Text label, string text, bool visible)
    {
        if (label == null)
        {
            return;
        }

        label.text = text;

        bool show = visible && !string.IsNullOrEmpty(text);

        if (label.gameObject.activeSelf != show)
        {
            label.gameObject.SetActive(show);
        }
    }

    // =========================================================
    // REFRESCO
    // =========================================================

    private void RefreshUI()
    {
        if (questManager == null)
        {
            ClearQuest();
            ClearSideQuest();
            RefreshLayout();
            return;
        }

        PlayQuestCompletedSound();

        if (questManager.TryGetActiveMainQuest(
            out QuestData questData,
            out NetworkQuestState questState))
        {
            trackedQuestIds.Add(questData.questID);
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
            trackedQuestIds.Add(sideData.questID);
            ShowSideQuest(sideData, sideState);
        }
        else
        {
            ClearSideQuest();
        }

        RefreshLayout();
    }

    /// <summary>
    /// Suena una vez cuando alguna de las misiones que se estaban mostrando
    /// pasa a Completada (AudioConfig > Mision Completada). Una mision que ya
    /// estaba completada al conectarse nunca se agrego, asi que no suena.
    /// </summary>
    private void PlayQuestCompletedSound()
    {
        if (trackedQuestIds.Count == 0)
            return;

        foreach (string id in trackedQuestIds)
        {
            if (questManager.TryGetQuestState(id, out NetworkQuestState state) &&
                state.State == QuestState.Completed)
            {
                completedBuffer.Add(id);
            }
        }

        if (completedBuffer.Count == 0)
            return;

        foreach (string id in completedBuffer)
            trackedQuestIds.Remove(id);

        completedBuffer.Clear();

        AudioManager.PlayUI(UISound.QuestComplete);
    }

    private void ShowSideQuest(
        QuestData questData,
        NetworkQuestState questState)
    {
        if (sideQuestPanel != null && !sideQuestPanel.activeSelf)
            sideQuestPanel.SetActive(true);

        SetLine(sideQuestNameText, questData.questName, expanded);
        SetLine(sideQuestObjectivesText, BuildObjectiveText(questData, questState), true);
        SetLine(sideQuestRewardsText, BuildRewardText(questData), expanded);
    }

    private void ClearSideQuest()
    {
        if (sideQuestPanel != null && sideQuestPanel.activeSelf)
            sideQuestPanel.SetActive(false);

        SetLine(sideQuestNameText, string.Empty, false);
        SetLine(sideQuestObjectivesText, string.Empty, false);
        SetLine(sideQuestRewardsText, string.Empty, false);
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

        SetLine(questTitleText, "Mision principal" + BuildToggleHint(), true);
        SetLine(questNameText, questData.questName, expanded);
        SetLine(questObjectivesText, BuildObjectiveText(questData, questState), true);
        SetLine(questRewardsText, BuildRewardText(questData), expanded);
    }

    private void ClearQuest()
    {
        SetLine(questTitleText, "No hay misiones activas", true);
        SetLine(questNameText, string.Empty, false);
        SetLine(questObjectivesText, string.Empty, false);
        SetLine(questRewardsText, string.Empty, false);
    }

    // =========================================================
    // TEXTOS
    // =========================================================

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
                        // ItemName es el nombre visible; .name es el del asset.
                        string itemName = !string.IsNullOrEmpty(reward.item.ItemName)
                            ? reward.item.ItemName
                            : reward.item.name;

                        result += itemName + " x" + reward.amount;
                    }
                    else
                    {
                        result += "Objeto x" + reward.amount;
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