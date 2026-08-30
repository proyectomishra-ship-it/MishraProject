using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Gestiona el oro del jugador.
/// 
/// El servidor es la única autoridad que puede modificarlo.
/// El valor se replica automáticamente a los clientes.
/// 
/// No forma parte de CharacterStats porque el oro es un recurso
/// económico y no una estadística de combate.
/// </summary>
public class GoldController : NetworkBehaviour
{
    // =========================================================
    // NETWORK
    // =========================================================

    private readonly NetworkVariable<int> netGold =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // =========================================================
    // EVENTS
    // =========================================================

    /// <summary>
    /// Se dispara cuando cambia la cantidad de oro.
    /// oldValue, newValue
    /// </summary>
    public event Action<int, int> OnGoldChanged;

    // =========================================================
    // GETTERS
    // =========================================================

    public int Gold => netGold.Value;

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        netGold.OnValueChanged += HandleGoldChanged;

        if (IsServer)
        {
            // Por ahora todos los jugadores comienzan con 0 oro.
            // Más adelante esto puede venir de datos persistentes.
            netGold.Value = 0;
        }
    }

    public override void OnNetworkDespawn()
    {
        netGold.OnValueChanged -= HandleGoldChanged;
    }

    // =========================================================
    // GOLD API
    // =========================================================

    /// <summary>
    /// Agrega oro al jugador.
    /// Solo puede ejecutarse en el servidor.
    /// </summary>
    public bool AddGold(int amount)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                $"[GoldController] AddGold llamado desde cliente en {name}."
            );

            return false;
        }

        if (amount <= 0)
            return false;

        netGold.Value += amount;

        Debug.Log(
            $"[Gold] {name} recibió {amount} oro. " +
            $"Total: {netGold.Value}"
        );

        return true;
    }

    /// <summary>
    /// Intenta quitar oro.
    /// Devuelve false si el jugador no tiene suficiente.
    /// </summary>
    public bool RemoveGold(int amount)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                $"[GoldController] RemoveGold llamado desde cliente en {name}."
            );

            return false;
        }

        if (amount <= 0)
            return false;

        if (netGold.Value < amount)
        {
            Debug.Log(
                $"[Gold] {name} no tiene suficiente oro. " +
                $"Tiene {netGold.Value}, necesita {amount}."
            );

            return false;
        }

        netGold.Value -= amount;

        Debug.Log(
            $"[Gold] {name} perdió {amount} oro. " +
            $"Total: {netGold.Value}"
        );

        return true;
    }

    /// <summary>
    /// Comprueba si el jugador posee determinada cantidad de oro.
    /// </summary>
    public bool HasGold(int amount)
    {
        if (amount < 0)
            return false;

        return netGold.Value >= amount;
    }

    // =========================================================
    // NETWORK EVENT
    // =========================================================

    private void HandleGoldChanged(int oldValue, int newValue)
    {
        OnGoldChanged?.Invoke(oldValue, newValue);

        Debug.Log(
            $"[Gold] {name} -> {oldValue} => {newValue}"
        );
    }
}