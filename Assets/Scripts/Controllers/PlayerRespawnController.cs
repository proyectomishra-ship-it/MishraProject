using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Controla el estado de muerte y respawn de un Player.
/// 
/// Responsabilidades:
/// - Mantener el estado IsDead.
/// - Guardar la posición de respawn.
/// - Ocultar/mostrar los renderers del Player.
/// - Bloquear/desbloquear el input.
/// - Detener el movimiento.
/// - Esperar el tiempo configurado.
/// - Restaurar los recursos.
/// - Aplicar una breve invulnerabilidad después del respawn.
/// 
/// El servidor es la única autoridad que inicia y ejecuta el respawn.
/// </summary>
public class PlayerRespawnController : NetworkBehaviour
{
    [Header("Respawn")]
    [SerializeField] private float respawnDelay = 5f;

    [Header("Protección post-respawn")]
    [SerializeField] private float postRespawnInvulnerability = 1.5f;

    private Player player;
    private MovementController movementController;
    private PlayerInputController inputController;

    private Renderer[] playerRenderers;

    private Vector3 respawnPosition;

    private Coroutine respawnCoroutine;

    private bool isInitialized;

    /// <summary>
    /// Indica si el Player está actualmente muerto.
    /// El servidor escribe. Los clientes pueden leer.
    /// </summary>
    public NetworkVariable<bool> IsDead = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// Indica si el Player está protegido contra daño después del respawn.
    /// </summary>
    public NetworkVariable<bool> IsInvulnerable = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// Posición que se utilizará para el próximo respawn.
    /// Actualmente será la posición donde murió.
    /// </summary>
    public Vector3 RespawnPosition => respawnPosition;

    private void Awake()
    {
        player = GetComponent<Player>();
        movementController = GetComponent<MovementController>();
        inputController = GetComponent<PlayerInputController>();

        playerRenderers = GetComponentsInChildren<Renderer>(true);

        if (player == null)
        {
            Debug.LogError(
                $"[PlayerRespawnController] {name} no tiene Player."
            );
        }

        if (movementController == null)
        {
            Debug.LogWarning(
                $"[PlayerRespawnController] {name} no tiene MovementController."
            );
        }

        if (inputController == null)
        {
            Debug.LogWarning(
                $"[PlayerRespawnController] {name} no tiene PlayerInputController."
            );
        }

        isInitialized = true;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // El estado visual inicial debe ser el de un jugador vivo.
        if (!IsDead.Value)
            SetPlayerRenderersVisible(true);
    }

    /// <summary>
    /// Inicia el proceso de muerte del Player.
    /// Solo puede ejecutarse en el servidor.
    /// </summary>
    public void StartDeath()
    {
        if (!IsServer)
            return;

        if (!isInitialized)
        {
            Debug.LogError(
                $"[PlayerRespawnController] {name} intentó morir antes de inicializarse."
            );
            return;
        }

        // Evita procesar la muerte dos veces.
        if (IsDead.Value)
        {
            Debug.LogWarning(
                $"[PlayerRespawnController] {name} ya está muerto. "
                + "Se ignora una segunda solicitud de muerte."
            );
            return;
        }

        // Guardamos la posición exacta donde murió.
        respawnPosition = transform.position;

        IsDead.Value = true;
        IsInvulnerable.Value = true;

        Debug.Log(
            $"[PlayerRespawnController] {name} murió. "
            + $"RespawnPosition={respawnPosition}"
        );

        // Detener completamente el movimiento.
        if (movementController != null)
            movementController.ResetMovementState();

        // Bloquear input.
        if (inputController != null)
            inputController.IsInputBlocked = true;

        // Ocultar el modelo, pero NO desactivar el Player.
        SetPlayerRenderersVisible(false);

        if (respawnCoroutine != null)
            StopCoroutine(respawnCoroutine);

        respawnCoroutine = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        if (!IsServer)
            yield break;

        Respawn();

        respawnCoroutine = null;
    }

    /// <summary>
    /// Ejecuta el respawn.
    /// </summary>
    private void Respawn()
    {
        if (!IsServer)
            return;

        if (!IsDead.Value)
            return;

        Debug.Log(
            $"[PlayerRespawnController] Respawneando {name} "
            + $"en {respawnPosition}"
        );

        // Actualmente respawnPosition coincide con el lugar de muerte.
        // Lo mantenemos explícito para poder cambiar esta estrategia
        // posteriormente por checkpoints, spawn points, etc.
        transform.position = respawnPosition;

        // Limpiar cualquier estado físico/movimiento residual.
        if (movementController != null)
            movementController.ResetMovementState();

        // Restaurar recursos al máximo.
        RestoreResources();

        // Player vuelve a estar vivo.
        IsDead.Value = false;

        // Mostrar nuevamente el modelo.
        SetPlayerRenderersVisible(true);

        // Desbloquear input.
        if (inputController != null)
            inputController.IsInputBlocked = false;

        Debug.Log(
            $"[PlayerRespawnController] {name} respawneó correctamente."
        );

        if (postRespawnInvulnerability > 0f)
        {
            StartCoroutine(PostRespawnProtectionRoutine());
        }
        else
        {
            IsInvulnerable.Value = false;
        }
    }

    /// <summary>
    /// Restaura HP, Mana y Stamina utilizando las APIs existentes.
    /// Esto permite que CharacterStatsSyncController reciba
    /// automáticamente los cambios mediante sus eventos.
    /// </summary>
    private void RestoreResources()
    {
        if (player == null)
            return;

        CharacterStats stats = player.GetStats();

        if (stats == null)
        {
            Debug.LogError(
                $"[PlayerRespawnController] No se pudieron restaurar "
                + $"los recursos de {name}: CharacterStats es null."
            );
            return;
        }

        ResourceController resources = player.GetResourceController();

        if (resources == null)
        {
            Debug.LogError(
                $"[PlayerRespawnController] No se pudieron restaurar "
                + $"los recursos de {name}: ResourceController es null."
            );
            return;
        }

        float healthMissing = stats.MaxHealth.Value - stats.CurrentHealth;
        float manaMissing = stats.MaxMana.Value - stats.CurrentMana;
        float staminaMissing = stats.Stamina.Value - stats.CurrentStamina;

        if (healthMissing > 0f)
            resources.Heal(healthMissing);

        if (manaMissing > 0f)
            resources.AddMana(manaMissing);

        if (staminaMissing > 0f)
            resources.RecoverStamina(staminaMissing);

        Debug.Log(
            $"[PlayerRespawnController] Recursos restaurados para {name}. "
            + $"HP={stats.CurrentHealth}/{stats.MaxHealth.Value}, "
            + $"Mana={stats.CurrentMana}/{stats.MaxMana.Value}, "
            + $"Stamina={stats.CurrentStamina}/{stats.Stamina.Value}"
        );
    }

    private IEnumerator PostRespawnProtectionRoutine()
    {
        yield return new WaitForSeconds(postRespawnInvulnerability);

        if (IsServer)
            IsInvulnerable.Value = false;

        Debug.Log(
            $"[PlayerRespawnController] Protección post-respawn finalizada para {name}."
        );
    }

    /// <summary>
    /// Oculta o muestra todos los Renderer pertenecientes al Player.
    /// No desactiva GameObjects ni componentes.
    /// </summary>
    private void SetPlayerRenderersVisible(bool visible)
    {
        if (playerRenderers == null)
            return;

        foreach (Renderer renderer in playerRenderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }
    }

    private void OnDestroy()
    {
        if (respawnCoroutine != null)
            StopCoroutine(respawnCoroutine);
    }
}