using UnityEngine;
using Unity.Netcode;

public class PlayerInputController : NetworkBehaviour
{
    private Player player;
    private PlayerInputActions inputActions;

    private Vector2 moveInput;
    private bool isSprinting;
    private bool isHoldingAttack;

 
    public void Initialize(Player player)
    {
        this.player = player;
    }

    public override void OnNetworkSpawn()
    {
      
        if (!IsOwner) return;

        inputActions = new PlayerInputActions();

        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        inputActions.Player.Jump.performed += ctx => player.Jump();

        inputActions.Player.Sprint.performed += ctx => isSprinting = true;
        inputActions.Player.Sprint.canceled += ctx => isSprinting = false;

        inputActions.Player.Attack.performed += ctx =>
        {
            isHoldingAttack = true;
            player.OnAttackPressed();
        };
        inputActions.Player.Attack.canceled += ctx =>
        {
            isHoldingAttack = false;
            player.OnAttackReleased();
        };

        inputActions.Player.SpecialAttack.performed += ctx => player.SpecialAttack();

        inputActions.Enable();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        inputActions?.Player.Move.Reset();
        inputActions?.Player.Jump.Reset();
        inputActions?.Player.Sprint.Reset();
        inputActions?.Player.Attack.Reset();
        inputActions?.Player.SpecialAttack.Reset();
        inputActions?.Disable();
        inputActions?.Dispose();
    }

    private void Update()
    {
        
        if (!IsOwner) return;
        if (player == null) return;

        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);

        if (move != Vector3.zero)
        {
            if (isSprinting) player.Run(move);
            else player.Move(move);
        }

        if (isHoldingAttack)
            player.OnAttackHeld();

        player.ApplyGravity();
    }
}