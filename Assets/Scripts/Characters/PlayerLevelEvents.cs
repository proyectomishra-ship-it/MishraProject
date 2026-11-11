using System;

/// <summary>
/// Eventos globales (solo servidor) sobre el nivel de los jugadores.
/// Los dispara Player; los escucha EnemyLevelScaler.
/// </summary>
public static class PlayerLevelEvents
{
    /// <summary>Un jugador subió de nivel (jugador, nuevo nivel).</summary>
    public static event Action<Player, int> OnPlayerLevelChanged;

    /// <summary>Un jugador apareció en el servidor (ya con su nivel actual).</summary>
    public static event Action<Player> OnPlayerJoined;

    /// <summary>Un jugador se fue / fue despawneado.</summary>
    public static event Action<Player> OnPlayerLeft;

    public static void RaiseLevelChanged(Player player, int newLevel)
        => OnPlayerLevelChanged?.Invoke(player, newLevel);

    public static void RaisePlayerJoined(Player player)
        => OnPlayerJoined?.Invoke(player);

    public static void RaisePlayerLeft(Player player)
        => OnPlayerLeft?.Invoke(player);
}
