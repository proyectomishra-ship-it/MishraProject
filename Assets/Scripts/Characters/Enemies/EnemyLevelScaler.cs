using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Mantiene el nivel promedio de los jugadores y ajusta el nivel de los enemigos
/// a (promedio +- rango).
///
/// - Escucha PlayerLevelEvents (subida de nivel, jugador entra / sale).
/// - Cuando cambia el promedio, reescala a todos los enemigos vivos.
/// - Un enemigo que nace o respawnea (EnemySpawnPoint) se registra solo y toma
///   el nivel vigente.
///
/// Solo actúa en el servidor. Se crea solo al iniciar el juego: no hay que
/// agregarlo a ninguna escena.
/// </summary>
public class EnemyLevelScaler : MonoBehaviour
{
    public static EnemyLevelScaler Instance { get; private set; }

    [Tooltip("Cada enemigo queda entre (promedio - rango) y (promedio + rango).")]
    [SerializeField] private int levelRange = 2;

    private readonly Dictionary<Player, int> playerLevels = new();
    private readonly HashSet<Enemy> enemies = new();
    private readonly List<Enemy> snapshot = new();

    /// <summary>Nivel promedio de los jugadores. 0 si no hay ninguno.</summary>
    public int AverageLevel { get; private set; }

    public event Action<int> OnAverageLevelChanged;

    // ---------------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        var go = new GameObject("EnemyLevelScaler");
        DontDestroyOnLoad(go);
        go.AddComponent<EnemyLevelScaler>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        PlayerLevelEvents.OnPlayerJoined += HandlePlayerJoined;
        PlayerLevelEvents.OnPlayerLeft += HandlePlayerLeft;
        PlayerLevelEvents.OnPlayerLevelChanged += HandlePlayerLevelChanged;
    }

    private void OnDisable()
    {
        PlayerLevelEvents.OnPlayerJoined -= HandlePlayerJoined;
        PlayerLevelEvents.OnPlayerLeft -= HandlePlayerLeft;
        PlayerLevelEvents.OnPlayerLevelChanged -= HandlePlayerLevelChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ---------------------------------------------------------------
    // PLAYERS
    // ---------------------------------------------------------------

    private void HandlePlayerJoined(Player player)
    {
        playerLevels[player] = player.GetLevel();
        Recalculate();
    }

    private void HandlePlayerLeft(Player player)
    {
        if (playerLevels.Remove(player))
            Recalculate();
    }

    private void HandlePlayerLevelChanged(Player player, int newLevel)
    {
        playerLevels[player] = newLevel;
        Recalculate();
    }

    private void Recalculate()
    {
        if (!IsServerRunning())
            return;

        int count = 0;
        int sum = 0;

        foreach (var kv in playerLevels)
        {
            if (kv.Key == null)
                continue;

            sum += kv.Value;
            count++;
        }

        int newAverage = count > 0
            ? Mathf.Max(1, Mathf.RoundToInt((float)sum / count))
            : 0;

        if (newAverage == AverageLevel)
            return;

        AverageLevel = newAverage;

        Debug.Log($"[EnemyLevel] Nivel promedio de jugadores: {AverageLevel} ({count} jugadores)");

        OnAverageLevelChanged?.Invoke(AverageLevel);

        ApplyToAll();
    }

    // ---------------------------------------------------------------
    // ENEMIES
    // ---------------------------------------------------------------

    /// <summary>Desfase aleatorio entre -rango y +rango (ambos incluidos).</summary>
    public static int RollLevelOffset()
    {
        int range = Instance != null ? Mathf.Max(0, Instance.levelRange) : 2;
        return UnityEngine.Random.Range(-range, range + 1);
    }

    public static void Register(Enemy enemy)
    {
        if (Instance == null || enemy == null)
            return;

        Instance.enemies.Add(enemy);

        if (Instance.AverageLevel > 0)
            enemy.ApplyAverageLevel(Instance.AverageLevel);
    }

    public static void Unregister(Enemy enemy)
    {
        if (Instance != null)
            Instance.enemies.Remove(enemy);
    }

    private void ApplyToAll()
    {
        snapshot.Clear();
        snapshot.AddRange(enemies);

        foreach (var enemy in snapshot)
        {
            if (enemy == null)
            {
                enemies.Remove(enemy);
                continue;
            }

            enemy.ApplyAverageLevel(AverageLevel);
        }
    }

    private static bool IsServerRunning()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsServer;
    }
}
