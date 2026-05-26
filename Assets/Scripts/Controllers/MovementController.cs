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
        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);

        // Movimiento horizontal — se aplica UNA vez por frame del servidor,
        // sin importar cuántos RPCs hayan llegado desde el cliente.
        if (_isMoving && _desiredDirection.sqrMagnitude > 0.01f)
        {
            character.transform.rotation = Quaternion.Slerp(
                character.transform.rotation,
                _desiredRotation,
                rotationSpeed * Time.deltaTime
            );
            controller.Move(_desiredDirection * _desiredSpeed * Time.deltaTime);
        }
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
}