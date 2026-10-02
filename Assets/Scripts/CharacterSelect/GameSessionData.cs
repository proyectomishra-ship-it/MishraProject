using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton persistente que vive entre escenas.
/// Guarda qué clase eligió cada cliente (clientId → className)
/// para que el servidor pueda spawnear el prefab correcto.
/// </summary>
public class GameSessionData : MonoBehaviour
{
    public static GameSessionData Instance { get; private set; }

    private readonly Dictionary<ulong, string> playerClasses = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetPlayerClass(ulong clientId, string className)
    {
        playerClasses[clientId] = className;
        Debug.Log($"[GameSessionData] ClientId {clientId} → {className}");
    }

    /// <summary>
    /// Devuelve true solo si ese cliente registro una clase. A diferencia de
    /// GetPlayerClass, no inventa un valor por defecto.
    /// </summary>
    public bool TryGetPlayerClass(ulong clientId, out string className)
        => playerClasses.TryGetValue(clientId, out className) && !string.IsNullOrEmpty(className);

    public string GetPlayerClass(ulong clientId)
    {
        return playerClasses.TryGetValue(clientId, out string cls) ? cls : "Warrior";
    }

    public void Clear() => playerClasses.Clear();
}