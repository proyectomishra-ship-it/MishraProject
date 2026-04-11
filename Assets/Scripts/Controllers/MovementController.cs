using UnityEngine;
using Unity.Netcode;

public class MovementController : NetworkBehaviour
{
    private Character character;
    private CharacterController controller;

    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float runMultiplier = 5f;

    [SerializeField] private float rotationSpeed = 10f;

    private float verticalVelocity = 0f;

    public void Initialize(Character character)
    {
        this.character = character;
        controller = character.GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!IsSpawned) return;
        if (controller == null) return;

        if (IsOwner || IsServer)
        {
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;

            verticalVelocity += gravity * Time.deltaTime;
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
        }
    }

    public void Move(Vector3 direction)
    {
        if (controller == null) return;
        if (!IsOwner && !IsServer) return;

        ApplyMovement(direction, speed);
    }

    public void Run(Vector3 direction)
    {
        if (controller == null) return;
        if (!IsOwner && !IsServer) return;

        ApplyMovement(direction, speed * runMultiplier);
    }

    public void Jump()
    {
        if (controller == null) return;
        if (!IsOwner && !IsServer) return;

        if (controller.isGrounded)
            verticalVelocity = jumpForce;
    }

    public void ApplyGravity() { }

   
    private void ApplyMovement(Vector3 direction, float currentSpeed)
    {
        controller.Move(direction * currentSpeed * Time.deltaTime);
        RotateTowards(direction);
    }


    private void RotateTowards(Vector3 direction)
    {
        if (direction == Vector3.zero) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        character.transform.rotation = Quaternion.Slerp(
            character.transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}