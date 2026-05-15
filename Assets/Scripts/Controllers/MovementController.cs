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

    public void Initialize(Character character)
    {
        this.character = character;
        controller = character.GetComponent<CharacterController>();
        stats = character.GetStats();
    }

    // Ya no necesitamos SetCameraYaw
    public void SetCameraYaw(float yaw) { }

    private void Update()
    {
        if (!IsSpawned || controller == null || !IsServer) return;

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;
        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    /// <summary>
    /// Recibe la dirección ya en world space (calculada en el cliente)
    /// y la rotación que debe tener el personaje.
    /// </summary>
    public void Move(Vector3 worldDirection, Quaternion targetRotation)
    {
        if (!IsServer || controller == null) return;
        ApplyMovement(worldDirection, targetRotation, stats.Speed.Value);
    }

    public void Run(Vector3 worldDirection, Quaternion targetRotation)
    {
        if (!IsServer || controller == null) return;
        ApplyMovement(worldDirection, targetRotation, stats.Speed.Value * runMultiplier);
    }

    // Overloads sin rotación para compatibilidad
    public void Move(Vector3 direction) => Move(direction, character.transform.rotation);
    public void Run(Vector3 direction) => Run(direction, character.transform.rotation);

    public void Jump()
    {
        if (!IsServer || controller == null) return;
        if (controller.isGrounded)
            verticalVelocity = jumpForce;
    }

    public void ApplyGravity() { }

    private void ApplyMovement(Vector3 worldDirection, Quaternion targetRotation, float speed)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < 0.01f) return;

        // Rotar suavemente hacia la rotación objetivo
        character.transform.rotation = Quaternion.Slerp(
            character.transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );

        controller.Move(worldDirection.normalized * speed * Time.deltaTime);
    }
}