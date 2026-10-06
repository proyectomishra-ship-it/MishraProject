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

    [Header("Rotación")]
    [SerializeField] private float rotationSpeed = 15f;

    private float verticalVelocity = 0f;

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
            motion += _desiredDirection * _desiredSpeed;
        }

        controller.Move(motion * Time.deltaTime);
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
            verticalVelocity = jumpForce;
    }

    public void ApplyGravity() { }

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

        Debug.Log($"[MovementController] {name} — estado de movimiento reseteado.");
    }
}