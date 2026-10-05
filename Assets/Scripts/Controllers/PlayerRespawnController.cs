using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Controla el estado de muerte y respawn de un Player.
/// 
/// Responsabilidades:
/// - Mantener el estado IsDead.
/// - Guardar la posici�n de respawn.
/// - Ocultar/mostrar los renderers del Player.
/// - Bloquear/desbloquear el input.
/// - Detener el movimiento.
/// - Esperar el tiempo configurado.
/// - Restaurar los recursos.
/// - Aplicar una breve invulnerabilidad despu�s del respawn.
/// 
/// El servidor es la �nica autoridad que inicia y ejecuta el respawn.
/// </summary>
public class PlayerRespawnController : NetworkBehaviour
{
    [Header("Respawn")]
    [SerializeField] private float respawnDelay = 5f;

    [Header("Protecci�n post-respawn")]
    [SerializeField] private float postRespawnInvulnerability = 1.5f;

    [Header("Visual")]
    [Tooltip("Ocultar el modelo del jugador (y su arma) al morir y volver a mostrarlo al " +
             "respawnear. Se aplica en TODOS los clientes a partir de IsDead.")]
    [SerializeField] private bool hideModelOnDeath = true;

    [Tooltip("Segundos que el modelo sigue visible despues de morir, para que se vea la " +
             "animacion de muerte. 0 = desaparece al instante. Debe ser menor que Respawn Delay.")]
    [SerializeField] private float hideDelayAfterDeath = 0f;

    private Player player;
    private MovementController movementController;
    private PlayerInputController inputController;



    private Vector3 respawnPosition;

    private Coroutine respawnCoroutine;
    private Coroutine hideCoroutine;

    // Renderers que ESTE script apago. Al volver a mostrar solo se reactivan
    // esos, para no encender renderers que el prefab tiene apagados a proposito.
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();

    private bool isInitialized;

    /// <summary>
    /// Indica si el Player est� actualmente muerto.
    /// El servidor escribe. Los clientes pueden leer.
    /// </summary>
    public NetworkVariable<bool> IsDead = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// Indica si el Player est� protegido contra da�o despu�s del respawn.
    /// </summary>
    public NetworkVariable<bool> IsInvulnerable = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// Posici�n que se utilizar� para el pr�ximo respawn.
    /// Actualmente ser� la posici�n donde muri�.
    /// </summary>
    public Vector3 RespawnPosition => respawnPosition;

    private void Awake()
    {
        player = GetComponent<Player>();
        movementController = GetComponent<MovementController>();
        inputController = GetComponent<PlayerInputController>();



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

        // El ocultar/mostrar visual se dispara desde IsDead, que se sincroniza
        // a todos. Antes se hacia solo en el servidor (StartDeath/Respawn), asi
        // que los clientes nunca veian desaparecer ni reaparecer al jugador.
        IsDead.OnValueChanged += HandleDeadChanged;

        // Jugador que ya estaba muerto cuando este cliente lo recibio.
        if (IsDead.Value)
            ApplyDeadVisual(true);
    }

    public override void OnNetworkDespawn()
    {
        IsDead.OnValueChanged -= HandleDeadChanged;
        CancelPendingHide();

        base.OnNetworkDespawn();
    }

    private void HandleDeadChanged(bool previous, bool current)
    {
        if (current)
            ApplyDeadVisual(false);
        else
            ApplyAliveVisual();
    }

    private void ApplyDeadVisual(bool immediate)
    {
        if (!hideModelOnDeath)
            return;

        CancelPendingHide();

        if (immediate || hideDelayAfterDeath <= 0f)
        {
            SetPlayerRenderersVisible(false);
            return;
        }

        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private void ApplyAliveVisual()
    {
        CancelPendingHide();
        SetPlayerRenderersVisible(true);
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(hideDelayAfterDeath);

        hideCoroutine = null;

        // Si respawneo mientras esperabamos, no ocultar.
        if (IsDead.Value)
            SetPlayerRenderersVisible(false);
    }

    private void CancelPendingHide()
    {
        if (hideCoroutine == null)
            return;

        StopCoroutine(hideCoroutine);
        hideCoroutine = null;
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
                $"[PlayerRespawnController] {name} intent� morir antes de inicializarse."
            );
            return;
        }

        // Evita procesar la muerte dos veces.
        if (IsDead.Value)
        {
            Debug.LogWarning(
                $"[PlayerRespawnController] {name} ya est� muerto. "
                + "Se ignora una segunda solicitud de muerte."
            );
            return;
        }

        // Guardamos la posici�n exacta donde muri�.
        respawnPosition = transform.position;

        IsDead.Value = true;
        IsInvulnerable.Value = true;

        Debug.Log(
            $"[PlayerRespawnController] {name} muri�. "
            + $"RespawnPosition={respawnPosition}"
        );

        // Detener completamente el movimiento.
        if (movementController != null)
            movementController.ResetMovementState();

        // Bloquear input.
        if (inputController != null)
            inputController.IsInputBlocked = true;

        // El modelo se oculta desde HandleDeadChanged (IsDead), en todos los
        // clientes. El Player NO se desactiva.

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
        // Lo mantenemos expl�cito para poder cambiar esta estrategia
        // posteriormente por checkpoints, spawn points, etc.
        transform.position = respawnPosition;

        // Limpiar cualquier estado f�sico/movimiento residual.
        if (movementController != null)
            movementController.ResetMovementState();

        // Restaurar recursos al m�ximo.
        RestoreResources();

        // Player vuelve a estar vivo.
        IsDead.Value = false;

        // El modelo se vuelve a mostrar desde HandleDeadChanged (IsDead = false).

        // Desbloquear input.
        if (inputController != null)
            inputController.IsInputBlocked = false;

        Debug.Log(
            $"[PlayerRespawnController] {name} respawne� correctamente."
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
    /// autom�ticamente los cambios mediante sus eventos.
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
            $"[PlayerRespawnController] Protecci�n post-respawn finalizada para {name}."
        );
    }

    /// <summary>
    /// Oculta o muestra los Renderer del Player (incluido el arma, que es hija
    /// del modelo). No desactiva GameObjects ni componentes.
    /// Al ocultar recuerda cuales apago, y al mostrar reactiva solo esos.
    /// </summary>
    private void SetPlayerRenderersVisible(bool visible)
    {
        if (visible)
        {
            foreach (Renderer renderer in hiddenRenderers)
            {
                if (renderer != null)
                    renderer.enabled = true;
            }

            hiddenRenderers.Clear();
            return;
        }

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && renderer.enabled)
            {
                renderer.enabled = false;
                hiddenRenderers.Add(renderer);
            }
        }
    }

    public override void OnDestroy()
    {
        if (respawnCoroutine != null)
            StopCoroutine(respawnCoroutine);

        base.OnDestroy();
    }
}