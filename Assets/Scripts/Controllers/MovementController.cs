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

    [SerializeField] private float rotationSpeed = 15f;

    private float verticalVelocity = 0f;

    public void Initialize(Character character)
    {
        this.character = character;
        controller = character.GetComponent<CharacterController>();
        stats = character.GetStats();
    }

    private void Update()
    {
        if (!IsSpawned) return;
        if (controller == null) return;

       
        if (!IsServer) return;

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;

        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    public void Move(Vector3 direction)
    {
        if (!IsServer) return;
        if (controller == null) return;

        ApplyMovement(direction, stats.Speed.Value);
    }

    public void Run(Vector3 direction)
    {
        if (!IsServer) return;
        if (controller == null) return;

        ApplyMovement(direction, stats.Speed.Value * runMultiplier);
    }

    public void Jump()
    {
        if (!IsServer) return;
        if (controller == null) return;

        if (controller.isGrounded)
            verticalVelocity = jumpForce;
    }

    public void ApplyGravity() { }

    private void ApplyMovement(Vector3 direction, float currentSpeed)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.01f)
        {
            direction = direction.normalized;

          
            RotateTowards(direction);

            controller.Move(direction * currentSpeed * Time.deltaTime);
        }
    }

    private void RotateTowards(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        character.transform.rotation = Quaternion.Slerp(
            character.transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}