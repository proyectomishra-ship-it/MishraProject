using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Mejoras de habilidades del jugador. Agregar este componente al prefab del Player
/// (junto a PlayerCombatController).
///
/// Flujo:
///  1. Servidor: detecta que el jugador subió de nivel (a partir de FirstPickLevel y cada 'Levels Per Upgrade'
///     niveles) y suma una mejora pendiente.
///  2. Servidor: sortea 3 mejoras de SU clase (sin repetir la que eligió la vez anterior) y se las manda
///     al dueño (SkillUpgradeOfferUI).
///  3. Dueño: elige una con el mouse (o con las teclas 1-3) y el servidor valida y la aplica.
///  4. Las mejoras elegidas se guardan como "cantidad de veces" (stacks) y se sincronizan a todos los
///     clientes, porque algunos efectos visuales (área de la onda, largo de la hoja) se dibujan en cada cliente.
///
/// El resto del código solo consulta Multiplier()/Mult(): no necesita saber cómo se eligió nada.
/// </summary>
public class PlayerSkillUpgrades : NetworkBehaviour
{
    public const int OfferSize = 3;

    private Player player;

    // Índice = (int)SkillUpgradeType. Solo escribe el servidor.
    private readonly NetworkList<int> stacks = new NetworkList<int>();

    // ---- Solo servidor ----
    private int lastLevel;
    private bool levelInitialized;
    private float nextCheck;
    private int pendingPicks;
    private int lastChosenType = -1;
    private readonly int[] currentOffer = new int[OfferSize];
    private int offerCount;
    private readonly List<SkillUpgradeType> candidates = new List<SkillUpgradeType>();

    // =========================================================
    // CONSULTAS (servidor y clientes)
    // =========================================================

    public static PlayerSkillUpgrades Of(Character owner)
        => owner != null ? owner.GetComponent<PlayerSkillUpgrades>() : null;

    /// <summary>Multiplicador de una mejora para este personaje. 1 si no tiene el componente o la mejora.</summary>
    public static float Mult(Character owner, SkillUpgradeType type)
    {
        PlayerSkillUpgrades upgrades = Of(owner);
        return upgrades != null ? upgrades.Multiplier(type) : 1f;
    }

    /// <summary>Cantidad entera que aporta una mejora (ej: flechas extra). 0 si no tiene el componente.</summary>
    public static int Count(Character owner, SkillUpgradeType type)
    {
        PlayerSkillUpgrades upgrades = Of(owner);
        return upgrades != null ? upgrades.ExtraCount(type) : 0;
    }

    public int GetStacks(SkillUpgradeType type)
    {
        int i = (int)type;
        return i >= 0 && i < stacks.Count ? stacks[i] : 0;
    }

    /// <summary>
    /// Factor a multiplicar. Las mejoras normales suman: +10% x 3 veces = 1.3.
    /// Las de reducción (recarga, costo de maná) son multiplicativas: -5% x 3 = 0.95^3, así nunca llegan a 0.
    /// </summary>
    public float Multiplier(SkillUpgradeType type)
    {
        int n = GetStacks(type);

        if (n <= 0 || !SkillUpgradeDatabase.Current.TryGet(type, out SkillUpgradeDatabase.Entry entry))
            return 1f;

        if (type.IsReduction())
            return Mathf.Pow(Mathf.Clamp01(1f - entry.valuePerStack), n);

        return 1f + entry.valuePerStack * n;
    }

    public int ExtraCount(SkillUpgradeType type)
    {
        int n = GetStacks(type);

        if (n <= 0 || !SkillUpgradeDatabase.Current.TryGet(type, out SkillUpgradeDatabase.Entry entry))
            return 0;

        return Mathf.RoundToInt(entry.valuePerStack * n);
    }

    /// <summary>Clase del jugador según su habilidad especial (o su pasiva si no tiene especial).</summary>
    public static UpgradeClass ClassKindOf(PlayerClassData classData)
    {
        if (classData == null)
            return UpgradeClass.None;

        SpecialAbility special = classData.SpecialAbility;

        if (special is MageShockwaveAbility) return UpgradeClass.Mage;
        if (special is WarriorBladeAbility) return UpgradeClass.Warrior;
        if (special is HunterBarrageAbility) return UpgradeClass.Hunter;

        PassiveAbility passive = classData.Passive;

        if (passive is BigFireballPassive) return UpgradeClass.Mage;
        if (passive is WarriorAuraPassive) return UpgradeClass.Warrior;
        if (passive is ExplosiveArrowPassive) return UpgradeClass.Hunter;

        return UpgradeClass.None;
    }

    /// <summary>¿Esta mejora es de la clase del jugador? (Evita ofrecer "más flechas" a un Mage.)</summary>
    public static bool AppliesTo(SkillUpgradeType type, PlayerClassData classData)
    {
        if (type.IsLegacy())
            return false;

        UpgradeClass kind = ClassKindOf(classData);

        if (kind == UpgradeClass.None || type.ClassOf() != kind)
            return false;

        // El costo de maná solo tiene sentido si la habilidad realmente consume maná.
        if (type == SkillUpgradeType.MageShockwaveManaCost)
            return classData.SpecialAbility != null && classData.SpecialAbility.ManaCost > 0f;

        return true;
    }

    /// <summary>Primer nivel que da una mejora para este jugador.</summary>
    public int FirstPickLevel
    {
        get
        {
            int configured = SkillUpgradeDatabase.Current.FirstPickLevel;

            if (configured > 0)
                return configured;

            PlayerClassData classData = player != null ? player.ClassData : null;

            if (classData == null)
                return int.MaxValue;

            int passiveLevel = classData.Passive != null ? classData.Passive.UnlockLevel : 1;

            // Las habilidades están desbloqueadas recién cuando se tienen las dos.
            return Mathf.Max(classData.SpecialUnlockLevel, passiveLevel) + 1;
        }
    }

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        player = GetComponent<Player>();

        if (IsServer)
        {
            int count = Enum.GetValues(typeof(SkillUpgradeType)).Length;

            while (stacks.Count < count)
                stacks.Add(0);
        }

        // Si el servidor ya tenía una oferta abierta (reconexión, UI creada tarde), se pide de nuevo.
        if (IsOwner)
            RequestOfferSyncServerRpc();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
            SkillUpgradeOfferUI.Hide();

        base.OnNetworkDespawn();
    }

    // =========================================================
    // SERVIDOR: DETECTAR SUBIDAS DE NIVEL
    // =========================================================

    private void Update()
    {
        if (!IsServer || !IsSpawned || player == null)
            return;

        // Se usa el tiempo real: con la partida pausada (Time.timeScale = 0) no debe quedar trabado.
        if (Time.unscaledTime < nextCheck)
            return;

        nextCheck = Time.unscaledTime + 0.25f;

        CharacterStats stats = player.GetStats();

        if (stats == null)
            return;

        if (!levelInitialized)
        {
            lastLevel = stats.Level;
            levelInitialized = true;
            return;
        }

        if (stats.Level < lastLevel)
        {
            lastLevel = stats.Level;
            return;
        }

        if (stats.Level == lastLevel)
            return;

        // Si se subieron varios niveles de golpe, se revisa cada uno: da mejora el que llegue al nivel mínimo
        // y caiga en el intervalo configurado ('Levels Per Upgrade').
        int first = FirstPickLevel;
        int step = SkillUpgradeDatabase.Current.LevelsPerUpgrade;
        int gained = 0;

        for (int level = lastLevel + 1; level <= stats.Level; level++)
        {
            if (level >= first && (level - first) % step == 0)
                gained++;
        }

        lastLevel = stats.Level;

        if (gained <= 0)
            return;

        pendingPicks += gained;

        Debug.Log($"[SkillUpgrades] {player.name} nivel {stats.Level}: +{gained} mejora(s), pendientes {pendingPicks}");

        // Si ya hay una oferta en pantalla, la siguiente sale cuando elija.
        if (offerCount == 0)
            RollNextOffer();
    }

    // =========================================================
    // SERVIDOR: SORTEO DE LA OFERTA
    // =========================================================

    private void RollNextOffer()
    {
        offerCount = 0;

        if (pendingPicks > 0)
        {
            BuildCandidates();

            if (candidates.Count == 0)
            {
                // Todas las mejoras al máximo: no queda nada que ofrecer.
                pendingPicks = 0;
            }
            else
            {
                int want = Mathf.Min(OfferSize, candidates.Count);

                for (int n = 0; n < want; n++)
                {
                    int index = PickWeighted();
                    currentOffer[offerCount++] = (int)candidates[index];
                    candidates.RemoveAt(index);
                }
            }
        }

        SendOffer();
    }

    private void BuildCandidates()
    {
        candidates.Clear();

        PlayerClassData classData = player != null ? player.ClassData : null;
        SkillUpgradeDatabase db = SkillUpgradeDatabase.Current;

        foreach (SkillUpgradeDatabase.Entry entry in db.Entries)
        {
            if (!entry.enabled || entry.weight <= 0f)
                continue;

            if (!AppliesTo(entry.type, classData))
                continue;

            if (GetStacks(entry.type) >= entry.maxStacks)
                continue;

            candidates.Add(entry.type);
        }

        // No se puede elegir lo mismo dos veces seguidas. Solo se ignora la regla si la mejora
        // anterior fuera la única que queda (para no dejar al jugador sin opciones).
        if (db.BlockRepeatOfLastPick && lastChosenType >= 0 && candidates.Count > 1)
            candidates.Remove((SkillUpgradeType)lastChosenType);
    }

    private int PickWeighted()
    {
        SkillUpgradeDatabase db = SkillUpgradeDatabase.Current;
        float total = 0f;

        for (int i = 0; i < candidates.Count; i++)
            total += WeightOf(db, candidates[i]);

        float roll = UnityEngine.Random.value * total;

        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= WeightOf(db, candidates[i]);

            if (roll <= 0f)
                return i;
        }

        return candidates.Count - 1;
    }

    private static float WeightOf(SkillUpgradeDatabase db, SkillUpgradeType type)
        => db.TryGet(type, out SkillUpgradeDatabase.Entry entry) ? entry.weight : 1f;

    // =========================================================
    // RED
    // =========================================================

    private void SendOffer()
    {
        int a = offerCount > 0 ? currentOffer[0] : -1;
        int b = offerCount > 1 ? currentOffer[1] : -1;
        int c = offerCount > 2 ? currentOffer[2] : -1;

        var rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };

        ShowOfferClientRpc(a, b, c, pendingPicks, rpcParams);
    }

    /// <summary>Solo el dueño. Con las 3 opciones en -1 (o sin pendientes) cierra el cartel.</summary>
    [ClientRpc]
    private void ShowOfferClientRpc(int a, int b, int c, int pending, ClientRpcParams rpcParams = default)
    {
        var types = new List<int>(OfferSize);

        if (a >= 0) types.Add(a);
        if (b >= 0) types.Add(b);
        if (c >= 0) types.Add(c);

        SkillUpgradeOfferUI.Show(this, types.ToArray(), pending);
    }

    [ServerRpc]
    private void RequestOfferSyncServerRpc()
    {
        SendOffer();
    }

    /// <summary>Lo llama el cartel cuando el jugador elige la opción 'slot' (0-2).</summary>
    public void ChooseUpgrade(int slot)
    {
        if (!IsOwner)
            return;

        ChooseUpgradeServerRpc(slot);
    }

    [ServerRpc]
    private void ChooseUpgradeServerRpc(int slot)
    {
        // El servidor es la autoridad: solo vale una opción de la oferta que él mismo sorteó.
        if (offerCount == 0 || slot < 0 || slot >= offerCount || pendingPicks <= 0)
            return;

        var type = (SkillUpgradeType)currentOffer[slot];

        if (SkillUpgradeDatabase.Current.TryGet(type, out SkillUpgradeDatabase.Entry entry) &&
            GetStacks(type) < entry.maxStacks)
        {
            stacks[(int)type] = GetStacks(type) + 1;
            lastChosenType = (int)type;

            Debug.Log($"[SkillUpgrades] {player.name} eligió {type} (x{GetStacks(type)})");
        }

        pendingPicks--;

        RollNextOffer();
    }
}
