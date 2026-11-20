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

    [Header("Side Quests")]
    [Tooltip("Máximo de misiones secundarias activas a la vez. 0 = sin límite.")]
    [Min(0)]
    [SerializeField]
    private int maxActiveSideQuests = 1;

    private readonly Dictionary<string, QuestData> questDatabase = new();

    private readonly Dictionary<string, QuestRuntimeState> activeQuests = new();

    private NetworkList<NetworkQuestState> networkQuests;

    // Solo servidor: quien aporto cuantas unidades de cada item a cada mision (collect / craft).
    // questID -> clientId -> item -> cantidad contada. Se usa para retirar los items al completar.
    private readonly Dictionary<string, Dictionary<ulong, Dictionary<ItemData, int>>> contributions = new();

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

        // Avisamos a cualquier sistema que ya est� esperando
        // informaci�n de las misiones.
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
        contributions.Clear();

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
                // La primera misi�n principal comienza activa.
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
                // para cuando implementemos aceptaci�n/rechazo.
                initialState = quest.startsActive
                    ? QuestState.Active
                    : QuestState.Available;
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

        if (quest.questType == QuestType.Side &&
            !HasFreeSideQuestSlot())
        {
            Debug.LogWarning(
                $"Quest '{questID}' cannot be accepted: " +
                "the side quest limit was reached.");

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
    // OBJECTIVE PROGRESS (kill / collect item / craft item)
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
                "ReportEnemyKilled received a null EnemyTypeData.");

            return;
        }

        ReportProgress(
            objective =>
                objective is KillObjectiveData kill &&
                kill.EnemyType == enemyType,
            1);
    }

    /// <summary>
    /// Un jugador encontró (recogió) un item. Solo servidor.
    /// </summary>
    public void ReportItemCollected(
        ItemData item,
        int amount = 1,
        Player contributor = null)
    {
        if (!IsServer || item == null || amount <= 0)
        {
            return;
        }

        ReportProgress(
            objective =>
                objective is CollectItemObjectiveData collect &&
                collect.Item == item,
            amount,
            item,
            contributor);
    }

    /// <summary>
    /// Un jugador crafteó un item. Solo servidor.
    /// </summary>
    public void ReportItemCrafted(
        ItemData item,
        int amount = 1,
        Player contributor = null)
    {
        if (!IsServer || item == null || amount <= 0)
        {
            return;
        }

        ReportProgress(
            objective =>
                objective is CraftItemObjectiveData craft &&
                craft.Item == item,
            amount,
            item,
            contributor);
    }

    /// <summary>
    /// Suma progreso a toda quest activa que tenga un objetivo que cumpla
    /// la condicion. Logica comun a kill / collect / craft.
    /// </summary>
    private void ReportProgress(
        Func<QuestObjectiveData, bool> matches,
        int amount,
        ItemData item = null,
        Player contributor = null)
    {
        foreach (QuestData quest in availableQuests)
        {
            if (quest == null || quest.objectives == null)
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

            // Las misiones principales todavia bloqueadas tambien acumulan
            // progreso: si el jugador derrota al jefe antes de que le toque,
            // esa baja cuenta cuando la mision se active. Las secundarias
            // solo cuentan mientras estan activas.
            bool isActive =
                networkState.State == QuestState.Active;

            bool isPreCredit =
                networkState.State == QuestState.Locked &&
                quest.questType == QuestType.Main;

            if (!isActive && !isPreCredit)
            {
                continue;
            }

            bool questChanged = false;

            foreach (QuestObjectiveData objective in quest.objectives)
            {
                if (objective == null || !matches(objective))
                {
                    continue;
                }

                int progressBefore = networkState.Progress;

                networkState.Progress =
                    Mathf.Min(
                        progressBefore + amount,
                        networkState.RequiredAmount);

                // Solo se registra lo que realmente conto (sin el excedente).
                int credited = networkState.Progress - progressBefore;

                if (credited > 0 && item != null && contributor != null)
                {
                    RecordContribution(
                        quest.questID,
                        contributor.OwnerClientId,
                        item,
                        credited);
                }

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

            // Una mision bloqueada solo guarda el progreso; se completa
            // al activarse (ver ActivateNextQuest).
            if (isActive &&
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

        // Primero se retiran los items (libera espacio) y despues se entregan las recompensas.
        ConsumeObjectiveItems(quest);

        GiveRewardsToAllPlayers(quest);

        if (quest.nextQuest != null)
        {
            ActivateNextQuest(
                quest.nextQuest);
        }
    }

    // =========================================================
    // CONSUMO DE ITEMS (collect / craft)
    // =========================================================

    private void RecordContribution(
        string questID,
        ulong clientId,
        ItemData item,
        int amount)
    {
        if (!contributions.TryGetValue(questID, out var byPlayer))
        {
            byPlayer = new Dictionary<ulong, Dictionary<ItemData, int>>();
            contributions[questID] = byPlayer;
        }

        if (!byPlayer.TryGetValue(clientId, out var byItem))
        {
            byItem = new Dictionary<ItemData, int>();
            byPlayer[clientId] = byItem;
        }

        byItem.TryGetValue(item, out int current);
        byItem[item] = current + amount;
    }

    /// <summary>
    /// Al completarse una mision, retira del inventario de cada jugador que aporto las unidades
    /// que se contaron para los objetivos de recoger / craftear (hasta lo requerido por el objetivo).
    /// Si el jugador ya no tiene alguna unidad (la uso, la crafteo en otra cosa), se retira lo que tenga.
    /// </summary>
    private void ConsumeObjectiveItems(QuestData quest)
    {
        if (!IsServer || quest == null || quest.objectives == null)
        {
            return;
        }

        if (!contributions.TryGetValue(quest.questID, out var byPlayer))
        {
            return;
        }

        foreach (QuestObjectiveData objective in quest.objectives)
        {
            ItemData item = null;
            bool consume = false;

            if (objective is CollectItemObjectiveData collect)
            {
                item = collect.Item;
                consume = collect.ConsumeOnComplete;
            }
            else if (objective is CraftItemObjectiveData craft)
            {
                item = craft.Item;
                consume = craft.ConsumeOnComplete;
            }

            if (!consume || item == null)
            {
                continue;
            }

            int remaining = objective.requiredAmount;

            foreach (var pair in byPlayer)
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (!pair.Value.TryGetValue(item, out int contributed) ||
                    contributed <= 0)
                {
                    continue;
                }

                Player player = FindPlayer(pair.Key);

                if (player == null)
                {
                    continue;
                }

                InventoryController inventory = player.GetInventory();

                if (inventory == null)
                {
                    continue;
                }

                int amount = Mathf.Min(
                    contributed,
                    remaining,
                    inventory.GetQuantity(item));

                if (amount <= 0)
                {
                    continue;
                }

                if (!inventory.RemoveItem(item, amount))
                {
                    continue;
                }

                remaining -= amount;

                UnequipIfGone(player, inventory, item);

                Debug.Log(
                    $"[Quest] Se retiraron {amount}x {item.name} " +
                    $"del jugador {pair.Key} por completar '{quest.questName}'.");
            }

            if (remaining > 0)
            {
                Debug.LogWarning(
                    $"[Quest] '{quest.questName}': faltaron retirar {remaining}x " +
                    $"{item.name} (los jugadores ya no los tenian).");
            }
        }

        contributions.Remove(quest.questID);
    }

    /// <summary>Los items equipados cuentan en el inventario: si ya no queda ninguno, se desequipa.</summary>
    private static void UnequipIfGone(
        Player player,
        InventoryController inventory,
        ItemData item)
    {
        if (item is not IEquippable equippable ||
            inventory.GetQuantity(item) > 0)
        {
            return;
        }

        EquipmentController equipment = player.GetEquipment();

        if (equipment != null &&
            equipment.GetEquipped(equippable.Slot) is ItemData equipped &&
            equipped == item)
        {
            equipment.Unequip(equippable.Slot);
        }
    }

    private static Player FindPlayer(ulong clientId)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.ConnectedClients.TryGetValue(
                clientId,
                out NetworkClient client) ||
            client.PlayerObject == null)
        {
            return null;
        }

        return client.PlayerObject.GetComponent<Player>();
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
                $"No se encontr� el NetworkClient " +
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
                        $"recibi� realmente {reward.amount} XP.");

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
                        $"recibi� realmente {reward.amount} Gold. " +
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
                        $"recibi� {reward.amount}x " +
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

        // Una mision bloqueada puede traer progreso acumulado (jefe derrotado
        // antes de tiempo): se conserva. En cualquier otro caso parte de cero.
        bool wasLocked =
            nextState.State == QuestState.Locked;

        nextState.State =
            QuestState.Active;

        nextState.RequiredAmount =
            GetTotalRequiredAmount(nextQuest);

        nextState.Progress = wasLocked
            ? Mathf.Min(
                nextState.Progress,
                nextState.RequiredAmount)
            : 0;

        Debug.Log(
            $"Next quest activated: " +
            $"{nextQuest.questName}");

        // Si el objetivo ya estaba cumplido, se completa de inmediato y se
        // encadena con la siguiente mision.
        if (nextState.RequiredAmount > 0 &&
            nextState.Progress >= nextState.RequiredAmount)
        {
            CompleteQuest(
                nextQuest,
                ref nextState);
        }

        networkQuests[index] =
            nextState;
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

    // ---------------------------------------------------------
    // Side quests (usado por el NPC que entrega misiones)
    // ---------------------------------------------------------

    public int ActiveSideQuestCount
    {
        get
        {
            int count = 0;

            foreach (QuestData candidate in availableQuests)
            {
                if (candidate == null ||
                    candidate.questType != QuestType.Side)
                {
                    continue;
                }

                if (TryGetQuestState(
                        candidate.questID,
                        out NetworkQuestState s) &&
                    s.State == QuestState.Active)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// true si se puede aceptar otra misión secundaria
    /// (según maxActiveSideQuests).
    /// </summary>
    public bool HasFreeSideQuestSlot()
    {
        return maxActiveSideQuests <= 0 ||
               ActiveSideQuestCount < maxActiveSideQuests;
    }

    /// <summary>
    /// Elige al azar una misión secundaria en estado Available.
    /// </summary>
    public bool TryGetRandomAvailableSideQuest(
        out QuestData quest)
    {
        var candidates = new List<QuestData>();

        foreach (QuestData candidate in availableQuests)
        {
            if (candidate == null ||
                candidate.questType != QuestType.Side)
            {
                continue;
            }

            if (TryGetQuestState(
                    candidate.questID,
                    out NetworkQuestState s) &&
                s.State == QuestState.Available)
            {
                candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0)
        {
            quest = null;
            return false;
        }

        quest = candidates[
            UnityEngine.Random.Range(0, candidates.Count)];

        return true;
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