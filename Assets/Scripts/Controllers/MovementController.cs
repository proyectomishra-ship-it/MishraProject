using UnityEngine;
using Unity.Netcode;

public class MovementController : NetworkBehaviour
{
    private Character character;
    private CharacterController controller;
    private CharacterStats stats;

    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float runMultiplier = 5f;

    [Header("Suelo")]
    [Tooltip("Al bajar escaleras o rampas, cuanto se pega el personaje al suelo en lugar de caer escalon por escalon. " +
             "Debe ser un poco mayor que la altura de un escalon. 0 = desactivado.")]
    [SerializeField] private float groundSnapDistance = 0.35f;

    [Header("Ataque")]
    [Tooltip("Velocidad de movimiento mientras dura un golpe: 0 = clavado en el lugar, 1 = sin cambio.")]
    [Range(0f, 1f)]
    [SerializeField] private float attackMoveMultiplier = 0.2f;
    [Tooltip("Maximo de segundos que un ataque limita el movimiento (un arma lenta no deja al personaje clavado mucho tiempo).")]
    [SerializeField] private float attackMoveMaxSeconds = 0.6f;

    [Header("Rotación")]
    [SerializeField] private float rotationSpeed = 15f;

    private float verticalVelocity = 0f;

    // ── Salto real ────────────────────────────────────────────────────────────
    // Lo escribe el servidor al saltar y lo leen todos los clientes. Sirve para
    // que la animacion de salto/caida solo se active cuando el jugador SALTA y no
    // cada vez que se separa un poco del suelo (escalones, pendientes, terreno).
    private readonly NetworkVariable<bool> isJumping = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Margen antes de poder dar el salto por terminado (isGrounded sigue en true
    // el primer frame despues de saltar).
    private const float JumpLandingGrace = 0.15f;
    private float jumpStartTime;

    /// <summary>True desde que el jugador salta hasta que vuelve a tocar el suelo.</summary>
    public bool IsJumping => isJumping.Value;

    // Hasta cuando dura la limitacion de movimiento por un ataque. Solo servidor.
    private float attackMoveUntil;

    // Empuje externo (ej: golpe de area del boss). Solo servidor.
    private Vector3 knockbackVelocity;
    private float knockbackTimeLeft;
    private float knockbackDuration;

    /// <summary>Multiplicador de velocidad al correr. Lo lee PlayerAnimationController.</summary>
    public float RunMultiplier => runMultiplier;

    // ── Estado de movimiento ──────────────────────────────────────────────────
    // El servidor guarda el último input recibido y lo aplica en su propio
    // Update(), desacoplado del framerate/latencia del cliente.
    // Así un cliente a 120 fps no mueve al personaje el doble de rápido
    // que uno a 60 fps.
    // ─────────────────────────────────────────────────────────────────────────
    private Vector3 _desiredDirection = Vector3.zero;
    private Quaternion _desiredRotation = Quaternion.identity;
    private float _desiredSpeed = 0f;
    private bool _isMoving = false;

    public void Initialize(Character character)
    {
        this.character = character;
        controller = character.GetComponent<CharacterController>();
        stats = character.GetStats();
    }

    public void SetCameraYaw(float yaw) { }

    private void Update()
    {
        if (!IsSpawned || controller == null || !IsServer) return;

        // Estado de suelo del frame anterior (resultado de la ultima Move).
        bool wasGrounded = controller.isGrounded;

        // Gravedad
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        // IMPORTANTE: una SOLA llamada a controller.Move por frame.
        // CharacterController.isGrounded refleja unicamente el resultado de la
        // ULTIMA Move. Antes habia dos (una vertical y otra horizontal): al
        // moverse, la ultima era la horizontal, que no empuja contra el suelo,
        // isGrounded daba false y Jump() se ignoraba. Quieto funcionaba porque
        // la ultima Move era la vertical. Combinando ambas, el movimiento
        // vertical siempre forma parte de la ultima Move.
        Vector3 motion = Vector3.up * verticalVelocity;

        // Movimiento horizontal — se aplica UNA vez por frame del servidor,
        // sin importar cuántos RPCs hayan llegado desde el cliente.
        if (_isMoving && _desiredDirection.sqrMagnitude > 0.01f)
        {
            character.transform.rotation = Quaternion.Slerp(
                character.transform.rotation,
                _desiredRotation,
                rotationSpeed * Time.deltaTime
            );
            // Mientras se balancea el arma no se corre a toda velocidad (se veria deslizar).
            float speedScale = Time.time < attackMoveUntil ? attackMoveMultiplier : 1f;
            motion += _desiredDirection * _desiredSpeed * speedScale;
        }

        // Empuje: arranca rapido y frena hasta 0, recorriendo exactamente la distancia pedida.
        if (knockbackTimeLeft > 0f)
        {
            motion += knockbackVelocity * (knockbackTimeLeft / knockbackDuration);
            knockbackTimeLeft -= Time.deltaTime;
        }

        controller.Move(motion * Time.deltaTime);

        // Pegar al suelo. Al bajar escaleras o rampas rapido, el personaje se pasa del borde del
        // escalon y "cae" varios frames (la gravedad parte de -2 m/s), y la animacion lo toma como un
        // salto. Si hace un instante estaba apoyado y ahora no esta saltando, se lo baja hasta el
        // suelo cercano en este mismo frame. La ultima Move sigue siendo vertical, asi que
        // isGrounded queda bien para Jump().
        if (groundSnapDistance > 0f && wasGrounded && !controller.isGrounded && verticalVelocity <= 0f)
            controller.Move(Vector3.down * groundSnapDistance);

        // Fin del salto: ya paso el despegue y el controlador volvio a tocar suelo.
        // (Va despues del "pegar al suelo" para leer el isGrounded final del frame.)
        if (isJumping.Value &&
            Time.time - jumpStartTime > JumpLandingGrace &&
            controller.isGrounded)
        {
            isJumping.Value = false;
        }
    }

    /// <summary>
    /// Empuja al personaje 'distance' metros en 'direction' (plano horizontal) durante 'duration' segundos.
    /// Solo servidor. El jugador puede seguir moviendose mientras tanto.
    /// </summary>
    public void ApplyKnockback(Vector3 direction, float distance, float duration)
    {
        if (!IsServer || controller == null) return;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f || distance <= 0f) return;

        knockbackDuration = Mathf.Max(0.05f, duration);
        knockbackTimeLeft = knockbackDuration;
        // Velocidad inicial 2*d/T con caida lineal a 0: el area bajo la curva es 'distance'.
        knockbackVelocity = direction.normalized * (2f * distance / knockbackDuration);
    }

    /// <summary>
    /// Guarda la dirección/velocidad deseadas.
    /// El movimiento real se aplica en Update() — frame-rate independiente.
    /// </summary>
    public void Move(Vector3 worldDirection, Quaternion targetRotation)
    {
        if (!IsServer || controller == null) return;
        SetMovementState(worldDirection, targetRotation, stats.Speed.Value);
    }

    public void Run(Vector3 worldDirection, Quaternion targetRotation)
    {
        if (!IsServer || controller == null) return;
        SetMovementState(worldDirection, targetRotation, stats.Speed.Value * runMultiplier);
    }

    /// <summary>
    /// Detiene el movimiento horizontal. Llamar cuando el input vuelve a cero.
    /// </summary>
    public void Stop()
    {
        _isMoving = false;
        _desiredDirection = Vector3.zero;
        _desiredSpeed = 0f;
    }

    // Overloads sin rotación para compatibilidad con código existente
    public void Move(Vector3 direction) => Move(direction, character.transform.rotation);
    public void Run(Vector3 direction) => Run(direction, character.transform.rotation);

    public void Jump()
    {
        if (!IsServer || controller == null) return;
        if (controller.isGrounded)
        {
            verticalVelocity = jumpForce;
            jumpStartTime = Time.time;
            isJumping.Value = true;
        }
    }

    public void ApplyGravity() { }

    /// <summary>
    /// Frena el movimiento durante un ataque (ver attackMoveMultiplier). Lo llama el combate
    /// cada vez que inicia un golpe. Solo servidor.
    /// </summary>
    public void LimitMovementForAttack(float seconds)
    {
        if (!IsServer) return;

        float until = Time.time + Mathf.Min(seconds, attackMoveMaxSeconds);
        if (until > attackMoveUntil)
            attackMoveUntil = until;
    }

    private void SetMovementState(Vector3 worldDirection, Quaternion targetRotation, float speed)
    {
        worldDirection.y = 0f;
        _desiredDirection = worldDirection.normalized;
        _desiredRotation = targetRotation;
        _desiredSpeed = speed;
        _isMoving = worldDirection.sqrMagnitude > 0.01f;
    }


    /// <summary>
    /// Limpia completamente el estado de movimiento.
    /// Se utiliza al morir y al respawnear para evitar
    /// conservar velocidad, dirección o velocidad vertical residual.
    /// </summary>
    public void ResetMovementState()
    {
        if (!IsServer)
            return;

        _desiredDirection = Vector3.zero;
        _desiredRotation = Quaternion.identity;
        _desiredSpeed = 0f;
        _isMoving = false;
        verticalVelocity = 0f;
        knockbackTimeLeft = 0f;
        isJumping.Value = false;
        attackMoveUntil = 0f;

        Debug.Log($"[MovementController] {name} — estado de movimiento reseteado.");
    }
}