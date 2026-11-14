using UnityEngine;

/// <summary>
/// Habilidad pasiva de clase. Se activa sola cuando el jugador alcanza 'Unlock Level'
/// (editable en el Inspector de cada asset). Se asigna en PlayerClassData > Passive Ability.
/// Todo se hace con lo que ya existe en el juego: sin arte ni animaciones nuevas.
/// </summary>
public abstract class PassiveAbility : ScriptableObject
{
    [Header("Desbloqueo")]
    [Tooltip("Nivel al que se activa la pasiva automáticamente.")]
    [SerializeField, Min(1)] private int unlockLevel = 3;

    public int UnlockLevel => unlockLevel;

    /// <summary>Nombre para mostrar en avisos.</summary>
    public virtual string DisplayName => name;

    /// <summary>
    /// Servidor: modifica un proyectil de ataque básico/pesado del jugador justo antes de
    /// que aparezca en el mundo. Solo se llama si la pasiva ya está desbloqueada.
    /// </summary>
    public virtual void ConfigureProjectile(
        Player owner,
        GameObject instance,
        NetworkProjectile projectile,
        ref float damage)
    {
    }

    /// <summary>Servidor: estado propio por jugador (para pasivas que necesitan un "tick"). null = no necesita.</summary>
    public virtual PassiveRuntime CreateRuntime(Player owner) => null;

    /// <summary>Todos los clientes: muestra u oculta el efecto visual permanente de la pasiva.</summary>
    public virtual void SetVisualActive(Player owner, bool active)
    {
    }
}

/// <summary>Estado por jugador de una pasiva. Lo crea y actualiza PlayerCombatController (servidor).</summary>
public abstract class PassiveRuntime
{
    /// <summary>Se llama varias veces por segundo. 'active' = desbloqueada y jugador vivo.</summary>
    public abstract void Tick(bool active);

    /// <summary>Limpieza al salir el jugador (devuelve los bonus aplicados).</summary>
    public abstract void Dispose();
}
