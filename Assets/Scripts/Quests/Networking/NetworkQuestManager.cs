using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkQuestManager : NetworkBehaviour
{
    public static NetworkQuestManager Instance { get; private set; }

    [Header("Available Quests")]
    [SerializeField]
    private QuestData[] availableQuests;

    private readonly Dictionary<string, QuestData> questDatabase = new();

    private NetworkList<NetworkQuestState> networkQuests;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        networkQuests = new NetworkList<NetworkQuestState>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        BuildQuestDatabase();

        if (IsServer)
        {
            InitializeQuests();
        }

        networkQuests.OnListChanged += OnNetworkQuestListChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (networkQuests != null)
            networkQuests.OnListChanged -= OnNetworkQuestListChanged;

        base.OnNetworkDespawn();
    }

    // =========================================================
    // QUEST DATABASE
    // =========================================================

    private void BuildQuestDatabase()
    {
        questDatabase.Clear();

        foreach (QuestData quest in availableQuests)
        {
            if (quest == null)
                continue;

            if (string.IsNullOrWhiteSpace(quest.questID))
            {
                Debug.LogError(
                    $"Quest '{quest.name}' has no Quest ID.",
                    quest
                );

                continue;
            }

            if (questDatabase.ContainsKey(quest.questID))
            {
                Debug.LogError(
                    $"Duplicate Quest ID detected: {quest.questID}",
                    quest
                );

                continue;
            }

            questDatabase.Add(quest.questID, quest);
        }
    }

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void InitializeQuests()
    {
        networkQuests.Clear();

        foreach (QuestData quest in availableQuests)
        {
            if (quest == null)
                continue;

            QuestState initialState;

            // Por ahora ambas comienzan disponibles.
            // Más adelante las secundarias podrán ser ofrecidas
            // por la Guild y las principales podrán depender
            // de la progresión de la historia.
            initialState = QuestState.Available;

            NetworkQuestState networkState = new NetworkQuestState
            {
                QuestID = quest.questID,
                State = initialState,
                Progress = 0,
                RequiredAmount = GetTotalRequiredAmount(quest)
            };

            networkQuests.Add(networkState);
        }
    }

    private int GetTotalRequiredAmount(QuestData quest)
    {
        if (quest.objectives == null || quest.objectives.Length == 0)
            return 0;

        int amount = 0;

        foreach (QuestObjectiveData objective in quest.objectives)
        {
            if (objective == null)
                continue;

            amount += objective.requiredAmount;
        }

        return amount;
    }

    // =========================================================
    // QUEST ACCEPTANCE
    // =========================================================

    public void RequestAcceptQuest(string questID)
    {
        if (!IsClient)
            return;

        AcceptQuestServerRpc(
            new FixedString128Bytes(questID)
        );
    }

    [Rpc(
        SendTo.Server,
        InvokePermission = RpcInvokePermission.Everyone
    )]
    private void AcceptQuestServerRpc(
        FixedString128Bytes questID)
    {
        string id = questID.ToString();

        if (!questDatabase.TryGetValue(id, out QuestData quest))
        {
            Debug.LogWarning(
                $"Quest '{id}' does not exist."
            );

            return;
        }

        int index = FindNetworkQuestIndex(id);

        if (index < 0)
            return;

        NetworkQuestState currentState = networkQuests[index];

        if (currentState.State != QuestState.Available)
        {
            Debug.LogWarning(
                $"Quest '{id}' cannot be accepted because " +
                $"its state is {currentState.State}."
            );

            return;
        }

        currentState.State = QuestState.Active;

        networkQuests[index] = currentState;

        Debug.Log(
            $"Quest '{quest.questName}' accepted globally."
        );
    }

    // =========================================================
    // KILL OBJECTIVE
    // =========================================================

    public void ReportEnemyKilled(string enemyTypeID)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "ReportEnemyKilled must be called on the server."
            );

            return;
        }

        foreach (QuestData quest in availableQuests)
        {
            if (quest == null)
                continue;

            int questIndex = FindNetworkQuestIndex(quest.questID);

            if (questIndex < 0)
                continue;

            NetworkQuestState networkState =
                networkQuests[questIndex];

            if (networkState.State != QuestState.Active)
                continue;

            if (quest.objectives == null)
                continue;

            bool questChanged = false;

            for (int i = 0; i < quest.objectives.Length; i++)
            {
                QuestObjectiveData objective =
                    quest.objectives[i];

                if (objective is not KillObjectiveData killObjective)
                    continue;

                if (killObjective.enemyTypeID != enemyTypeID)
                    continue;

                int newProgress =
                    networkState.Progress + 1;

                newProgress = Mathf.Min(
                    newProgress,
                    networkState.RequiredAmount
                );

                networkState.Progress = newProgress;

                questChanged = true;

                Debug.Log(
                    $"Quest '{quest.questName}' progress: " +
                    $"{networkState.Progress}/" +
                    $"{networkState.RequiredAmount}"
                );

                break;
            }

            if (!questChanged)
                continue;

            if (networkState.Progress >=
                networkState.RequiredAmount)
            {
                CompleteQuest(
                    quest,
                    ref networkState
                );
            }

            networkQuests[questIndex] = networkState;
        }
    }

    // =========================================================
    // QUEST COMPLETION
    // =========================================================

    private void CompleteQuest(
        QuestData quest,
        ref NetworkQuestState networkState)
    {
        networkState.State = QuestState.Completed;

        Debug.Log(
            $"QUEST COMPLETED: {quest.questName}"
        );

        GiveRewardsToAllPlayers(quest);

        if (quest.nextQuest != null)
        {
            ActivateNextQuest(quest.nextQuest);
        }
    }

    // =========================================================
    // REWARDS
    // =========================================================

    private void GiveRewardsToAllPlayers(
        QuestData quest)
    {
        if (!IsServer)
            return;

        foreach (
            KeyValuePair<ulong, NetworkClient> clientPair
            in NetworkManager.Singleton.ConnectedClients)
        {
            ulong clientId = clientPair.Key;

            GiveRewardsToPlayer(
                clientId,
                quest
            );
        }
    }

    private void GiveRewardsToPlayer(
        ulong clientId,
        QuestData quest)
    {
        Debug.Log(
            $"Giving rewards from '{quest.questName}' " +
            $"to player {clientId}."
        );

        if (quest.rewards == null)
            return;

        foreach (QuestRewardData reward in quest.rewards)
        {
            if (reward == null)
                continue;

            switch (reward.rewardType)
            {
                case QuestRewardType.Experience:

                    Debug.Log(
                        $"Player {clientId} receives " +
                        $"{reward.amount} XP."
                    );

                    // TODO:
                    // Conectar con Player.AddExp()

                    break;

                case QuestRewardType.Gold:

                    Debug.Log(
                        $"Player {clientId} receives " +
                        $"{reward.amount} Gold."
                    );

                    // TODO:
                    // Conectar con GoldController.

                    break;
            }
        }
    }

    // =========================================================
    // NEXT QUEST
    // =========================================================

    private void ActivateNextQuest(
        QuestData nextQuest)
    {
        if (nextQuest == null)
            return;

        int index = FindNetworkQuestIndex(
            nextQuest.questID
        );

        if (index < 0)
        {
            Debug.LogWarning(
                $"Next quest '{nextQuest.questID}' " +
                $"is not registered."
            );

            return;
        }

        NetworkQuestState nextState =
            networkQuests[index];

        if (nextState.State != QuestState.Locked &&
            nextState.State != QuestState.Available)
        {
            return;
        }

        nextState.State = QuestState.Available;

        networkQuests[index] = nextState;

        Debug.Log(
            $"Next quest available: {nextQuest.questName}"
        );
    }

    // =========================================================
    // NETWORK HELPERS
    // =========================================================

    private int FindNetworkQuestIndex(
        string questID)
    {
        for (int i = 0; i < networkQuests.Count; i++)
        {
            if (networkQuests[i].QuestID.ToString() == questID)
            {
                return i;
            }
        }

        return -1;
    }

    private void OnNetworkQuestListChanged(
        NetworkListEvent<NetworkQuestState> changeEvent)
    {
        NetworkQuestState state;

        switch (changeEvent.Type)
        {
            case NetworkListEvent<NetworkQuestState>.EventType.Add:

                state = changeEvent.Value;

                Debug.Log(
                    $"Quest added: {state.QuestID}"
                );

                break;

            case NetworkListEvent<NetworkQuestState>.EventType.Value:

                state = changeEvent.Value;

                Debug.Log(
                    $"Quest updated: {state.QuestID} " +
                    $"[{state.State}] " +
                    $"{state.Progress}/" +
                    $"{state.RequiredAmount}"
                );

                break;

            case NetworkListEvent<NetworkQuestState>.EventType.Clear:

                Debug.Log(
                    "Quest list cleared."
                );

                break;

            case NetworkListEvent<NetworkQuestState>.EventType.Remove:

                Debug.Log(
                    "Quest removed."
                );

                break;

            case NetworkListEvent<NetworkQuestState>.EventType.Insert:

                state = changeEvent.Value;

                Debug.Log(
                    $"Quest inserted: {state.QuestID}"
                );

                break;
        }
    }

    // =========================================================
    // PUBLIC READ API
    // =========================================================

    public bool TryGetQuestState(
        string questID,
        out NetworkQuestState state)
    {
        int index = FindNetworkQuestIndex(questID);

        if (index >= 0)
        {
            state = networkQuests[index];
            return true;
        }

        state = default;
        return false;
    }
}

// =============================================================
// NETWORK QUEST STATE
// =============================================================

public struct NetworkQuestState :
    INetworkSerializable,
    System.IEquatable<NetworkQuestState>
{
    public FixedString128Bytes QuestID;

    public QuestState State;

    public int Progress;

    public int RequiredAmount;

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref QuestID);
        serializer.SerializeValue(ref State);
        serializer.SerializeValue(ref Progress);
        serializer.SerializeValue(ref RequiredAmount);
    }

    public bool Equals(NetworkQuestState other)
    {
        return QuestID.Equals(other.QuestID) &&
               State == other.State &&
               Progress == other.Progress &&
               RequiredAmount == other.RequiredAmount;
    }
}