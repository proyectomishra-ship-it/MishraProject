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

    private float verticalVelocity = 0f;

    public void Initialize(Character character)
    {
        this.character = character;
        controller = character.GetComponent<CharacterController>();
    }

    // -------------------------
    // MOVEMENT
    // -------------------------

  
    public void Move(Vector3 direction)
    {
        if (controller == null) return;

        if (IsOwner || IsServer)
            controller.Move(direction * speed * Time.deltaTime);
    }

    public void Run(Vector3 direction)
    {
        if (controller == null) return;

        if (IsOwner || IsServer)
            controller.Move(direction * speed * runMultiplier * Time.deltaTime);
    }

    public void Jump()
    {
        if (controller == null) return;

        if (IsOwner || IsServer)
        {
            if (controller.isGrounded)
                verticalVelocity = jumpForce;
        }
    }

    public void ApplyGravity()
    {
        if (controller == null) return;

     
        if (IsOwner || IsServer)
        {
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;

            verticalVelocity += gravity * Time.deltaTime;
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
        }
    }
}