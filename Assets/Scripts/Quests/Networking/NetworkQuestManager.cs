using System;
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

    private readonly Dictionary<string, QuestRuntimeState> activeQuests = new();

    private NetworkList<NetworkQuestState> networkQuests;

    // Evento utilizado por la UI para saber que debe actualizarse.
    public event Action OnQuestDataChanged;

    // =========================================================
    // UNITY
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

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        BuildQuestDatabase();

        if (IsServer)
        {
            InitializeQuests();
        }

        networkQuests.OnListChanged += OnNetworkQuestListChanged;

        // Avisamos a cualquier sistema que ya esté esperando
        // información de las misiones.
        OnQuestDataChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        if (networkQuests != null)
        {
            networkQuests.OnListChanged -= OnNetworkQuestListChanged;
        }

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
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(quest.questID))
            {
                Debug.LogError(
                    $"Quest '{quest.name}' has no Quest ID.",
                    quest);

                continue;
            }

            if (questDatabase.ContainsKey(quest.questID))
            {
                Debug.LogError(
                    $"Duplicate Quest ID detected: {quest.questID}",
                    quest);

                continue;
            }

            questDatabase.Add(
                quest.questID,
                quest);
        }
    }

    // =========================================================
    // INITIALIZE QUESTS
    // =========================================================

    private void InitializeQuests()
    {
        networkQuests.Clear();

        bool mainQuestActivated = false;

        foreach (QuestData quest in availableQuests)
        {
            if (quest == null)
            {
                continue;
            }

            QuestState initialState;

            if (quest.questType == QuestType.Main)
            {
                // La primera misión principal comienza activa.
                // Las siguientes quedan bloqueadas hasta que
                // la anterior sea completada.
                if (!mainQuestActivated)
                {
                    initialState = QuestState.Active;
                    mainQuestActivated = true;
                }
                else
                {
                    initialState = QuestState.Locked;
                }
            }
            else
            {
                // Las side quests las dejaremos disponibles
                // para cuando implementemos aceptación/rechazo.
                initialState = QuestState.Available;
            }

            NetworkQuestState networkState =
                new NetworkQuestState
                {
                    QuestID = quest.questID,
                    State = initialState,
                    Progress = 0,
                    RequiredAmount =
                        GetTotalRequiredAmount(quest)
                };

            networkQuests.Add(networkState);
        }
    }

    private int GetTotalRequiredAmount(
        QuestData quest)
    {
        if (quest.objectives == null ||
            quest.objectives.Length == 0)
        {
            return 0;
        }

        int amount = 0;

        foreach (
            QuestObjectiveData objective
            in quest.objectives)
        {
            if (objective == null)
            {
                continue;
            }

            amount += objective.requiredAmount;
        }

        return amount;
    }

    // =========================================================
    // QUEST ACCEPTANCE
    // =========================================================

    public void RequestAcceptQuest(
        string questID)
    {
        if (!IsClient)
        {
            return;
        }

        AcceptQuestServerRpc(questID);
    }

    [Rpc(
        SendTo.Server,
        InvokePermission = RpcInvokePermission.Everyone)]
    private void AcceptQuestServerRpc(
        string questID)
    {
        if (!IsServer)
        {
            return;
        }

        if (!questDatabase.TryGetValue(
                questID,
                out QuestData quest))
        {
            Debug.LogWarning(
                $"Quest '{questID}' does not exist.");

            return;
        }

        int index =
            FindNetworkQuestIndex(questID);

        if (index < 0)
        {
            return;
        }

        NetworkQuestState currentState =
            networkQuests[index];

        if (currentState.State != QuestState.Available)
        {
            Debug.LogWarning(
                $"Quest '{questID}' cannot be accepted " +
                $"because its state is {currentState.State}.");

            return;
        }

        currentState.State =
            QuestState.Active;

        networkQuests[index] =
            currentState;

        Debug.Log(
            $"Quest '{quest.questName}' accepted globally.");
    }

    // =========================================================
    // KILL OBJECTIVE
    // =========================================================

    public void ReportEnemyKilled(
        EnemyTypeData enemyType)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "ReportEnemyKilled must be called on the server.");

            return;
        }

        if (enemyType == null)
        {
            Debug.LogWarning(
                "ReportEnemyKilled recibió un EnemyTypeData null.");

            return;
        }

        foreach (QuestData quest in availableQuests)
        {
            if (quest == null)
            {
                continue;
            }

            int questIndex =
                FindNetworkQuestIndex(
                    quest.questID);

            if (questIndex < 0)
            {
                continue;
            }

            NetworkQuestState networkState =
                networkQuests[questIndex];

            if (networkState.State != QuestState.Active)
            {
                continue;
            }

            bool questChanged = false;

            if (quest.objectives == null)
            {
                continue;
            }

            for (
                int i = 0;
                i < quest.objectives.Length;
                i++)
            {
                QuestObjectiveData objective =
                    quest.objectives[i];

                if (objective is not KillObjectiveData killObjective)
                {
                    continue;
                }

                if (killObjective.EnemyType != enemyType)
                {
                    continue;
                }

                int newProgress =
                    networkState.Progress + 1;

                newProgress =
                    Mathf.Min(
                        newProgress,
                        networkState.RequiredAmount);

                networkState.Progress =
                    newProgress;

                questChanged = true;

                Debug.Log(
                    $"Quest '{quest.questName}' progress: " +
                    $"{networkState.Progress}/" +
                    $"{networkState.RequiredAmount}");

                break;
            }

            if (!questChanged)
            {
                continue;
            }

            if (
                networkState.Progress >=
                networkState.RequiredAmount)
            {
                CompleteQuest(
                    quest,
                    ref networkState);
            }

            networkQuests[questIndex] =
                networkState;
        }
    }

    // =========================================================
    // QUEST COMPLETION
    // =========================================================

    private void CompleteQuest(
        QuestData quest,
        ref NetworkQuestState networkState)
    {
        networkState.State =
            QuestState.Completed;

        Debug.Log(
            $"QUEST COMPLETED: {quest.questName}");

        GiveRewardsToAllPlayers(quest);

        if (quest.nextQuest != null)
        {
            ActivateNextQuest(
                quest.nextQuest);
        }
    }

    // =========================================================
    // REWARDS
    // =========================================================

    private void GiveRewardsToAllPlayers(
        QuestData quest)
    {
        if (!IsServer)
        {
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            return;
        }

        foreach (
            KeyValuePair<ulong, NetworkClient>
            clientPair
            in NetworkManager.Singleton.ConnectedClients)
        {
            ulong clientId =
                clientPair.Key;

            GiveRewardsToPlayer(
                clientId,
                quest);
        }
    }

    private void GiveRewardsToPlayer(
        ulong clientId,
        QuestData quest)
    {
        Debug.Log(
            $"Giving rewards from '{quest.questName}' " +
            $"to player {clientId}.");

        if (quest.rewards == null)
        {
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning(
                "NetworkManager.Singleton es null. " +
                "No se pueden entregar recompensas.");

            return;
        }

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
                clientId,
                out NetworkClient client))
        {
            Debug.LogWarning(
                $"No se encontró el NetworkClient " +
                $"para el jugador {clientId}.");

            return;
        }

        if (client.PlayerObject == null)
        {
            Debug.LogWarning(
                $"El PlayerObject del jugador {clientId} es null.");

            return;
        }

        GameObject playerObject =
            client.PlayerObject.gameObject;

        Player player =
            playerObject.GetComponent<Player>();

        if (player == null)
        {
            Debug.LogWarning(
                $"El PlayerObject del jugador {clientId} " +
                "no tiene componente Player.");

            return;
        }

        foreach (
            QuestRewardData reward
            in quest.rewards)
        {
            if (reward == null)
            {
                continue;
            }

            switch (reward.rewardType)
            {
                // =================================================
                // EXPERIENCE
                // =================================================

                case QuestRewardType.Experience:

                    Debug.Log(
                        $"Player {clientId} receives " +
                        $"{reward.amount} XP.");

                    player.AddExp(
                        reward.amount);

                    Debug.Log(
                        $"[Quest Reward] Player {clientId} " +
                        $"recibió realmente {reward.amount} XP.");

                    break;

                // =================================================
                // GOLD
                // =================================================

                case QuestRewardType.Gold:

                    Debug.Log(
                        $"Player {clientId} receives " +
                        $"{reward.amount} Gold.");

                    GoldController goldController =
                        playerObject.GetComponent<GoldController>();

                    if (goldController == null)
                    {
                        Debug.LogWarning(
                            $"El jugador {clientId} no tiene " +
                            $"un GoldController en su PlayerObject.");

                        break;
                    }

                    goldController.AddGold(
                        reward.amount);

                    Debug.Log(
                        $"[Quest Reward] Player {clientId} " +
                        $"recibió realmente {reward.amount} Gold. " +
                        $"Total: {goldController.Gold}");

                    break;

                // =================================================
                // ITEM
                // =================================================

                case QuestRewardType.Item:

                    if (reward.item == null)
                    {
                        Debug.LogWarning(
                            $"La quest '{quest.questName}' tiene " +
                            $"una recompensa de tipo Item pero " +
                            $"no tiene ItemData asignado.");

                        break;
                    }

                    InventoryController inventoryController =
                        playerObject.GetComponent<InventoryController>();

                    if (inventoryController == null)
                    {
                        Debug.LogWarning(
                            $"El jugador {clientId} no tiene " +
                            $"un InventoryController en su PlayerObject.");

                        break;
                    }

                    bool added =
                        inventoryController.AddItem(
                            reward.item,
                            reward.amount);

                    if (!added)
                    {
                        Debug.LogWarning(
                            $"[Quest Reward] No se pudo entregar " +
                            $"{reward.amount}x {reward.item.name} " +
                            $"al jugador {clientId}. " +
                            $"El inventario probablemente no tiene " +
                            $"espacio suficiente.");

                        break;
                    }

                    Debug.Log(
                        $"[Quest Reward] Player {clientId} " +
                        $"recibió {reward.amount}x " +
                        $"{reward.item.name}.");

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
        {
            return;
        }

        int index =
            FindNetworkQuestIndex(
                nextQuest.questID);

        if (index < 0)
        {
            Debug.LogWarning(
                $"Next quest '{nextQuest.questID}' " +
                "is not registered.");

            return;
        }

        NetworkQuestState nextState =
            networkQuests[index];

        if (
            nextState.State != QuestState.Locked &&
            nextState.State != QuestState.Available)
        {
            return;
        }

        nextState.State =
            QuestState.Active;

        nextState.Progress = 0;

        nextState.RequiredAmount =
            GetTotalRequiredAmount(nextQuest);

        networkQuests[index] =
            nextState;

        Debug.Log(
            $"Next quest activated: " +
            $"{nextQuest.questName}");
    }

    // =========================================================
    // NETWORK HELPERS
    // =========================================================

    private int FindNetworkQuestIndex(
        string questID)
    {
        for (
            int i = 0;
            i < networkQuests.Count;
            i++)
        {
            if (
                networkQuests[i]
                    .QuestID
                    .ToString() == questID)
            {
                return i;
            }
        }

        return -1;
    }

    private void OnNetworkQuestListChanged(
        NetworkListEvent<NetworkQuestState>
            changeEvent)
    {
        NetworkQuestState state;

        switch (changeEvent.Type)
        {
            case NetworkListEvent<NetworkQuestState>
                .EventType.Add:

                state =
                    changeEvent.Value;

                Debug.Log(
                    $"Quest added: {state.QuestID}");

                break;

            case NetworkListEvent<NetworkQuestState>
                .EventType.Value:

                state =
                    changeEvent.Value;

                Debug.Log(
                    $"Quest updated: {state.QuestID} " +
                    $"[{state.State}] " +
                    $"{state.Progress}/" +
                    $"{state.RequiredAmount}");

                break;

            case NetworkListEvent<NetworkQuestState>
                .EventType.Clear:

                Debug.Log(
                    "Quest list cleared.");

                break;

            case NetworkListEvent<NetworkQuestState>
                .EventType.Remove:

                Debug.Log(
                    "Quest removed.");

                break;

            case NetworkListEvent<NetworkQuestState>
                .EventType.Insert:

                state =
                    changeEvent.Value;

                Debug.Log(
                    $"Quest inserted: {state.QuestID}");

                break;
        }

        // Avisamos a la UI y a cualquier otro sistema
        // interesado en cambios de misiones.
        OnQuestDataChanged?.Invoke();
    }

    // =========================================================
    // PUBLIC READ API
    // =========================================================

    public bool TryGetQuestState(
        string questID,
        out NetworkQuestState state)
    {
        int index =
            FindNetworkQuestIndex(questID);

        if (index >= 0)
        {
            state =
                networkQuests[index];

            return true;
        }

        state = default;
        return false;
    }

    public bool TryGetQuestData(
        string questID,
        out QuestData quest)
    {
        if (questDatabase.TryGetValue(
                questID,
                out quest))
        {
            return true;
        }

        quest = null;
        return false;
    }

    public bool TryGetActiveMainQuest(
        out QuestData quest,
        out NetworkQuestState state)
    {
        foreach (QuestData candidate in availableQuests)
        {
            if (candidate == null)
            {
                continue;
            }

            if (candidate.questType != QuestType.Main)
            {
                continue;
            }

            if (!TryGetQuestState(
                    candidate.questID,
                    out NetworkQuestState candidateState))
            {
                continue;
            }

            if (candidateState.State != QuestState.Active)
            {
                continue;
            }

            quest = candidate;
            state = candidateState;

            return true;
        }

        quest = null;
        state = default;

        return false;
    }

    public bool TryGetActiveSideQuest(
        out QuestData quest,
        out NetworkQuestState state)
    {
        foreach (QuestData candidate in availableQuests)
        {
            if (candidate == null)
            {
                continue;
            }

            if (candidate.questType != QuestType.Side)
            {
                continue;
            }

            if (!TryGetQuestState(
                    candidate.questID,
                    out NetworkQuestState candidateState))
            {
                continue;
            }

            if (candidateState.State != QuestState.Active)
            {
                continue;
            }

            quest = candidate;
            state = candidateState;

            return true;
        }

        quest = null;
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
        serializer.SerializeValue(
            ref QuestID);

        serializer.SerializeValue(
            ref State);

        serializer.SerializeValue(
            ref Progress);

        serializer.SerializeValue(
            ref RequiredAmount);
    }

    public bool Equals(
        NetworkQuestState other)
    {
        return
            QuestID.Equals(other.QuestID) &&
            State == other.State &&
            Progress == other.Progress &&
            RequiredAmount == other.RequiredAmount;
    }
}