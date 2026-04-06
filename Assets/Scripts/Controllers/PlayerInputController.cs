using UnityEngine;

public class PlayerInputController : MonoBehaviour
{
    private Player player;
    private PlayerInputActions inputActions;
    private Vector2 moveInput;
    private bool isSprinting;
    private bool isHoldingAttack;

    public void Initialize(Player player)
    {
        this.player = player;
        inputActions = new PlayerInputActions();

        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        inputActions.Player.Jump.performed += ctx => player.Jump();

        inputActions.Player.Sprint.performed += ctx => isSprinting = true;
        inputActions.Player.Sprint.canceled += ctx => isSprinting = false;

        inputActions.Player.Attack.performed += ctx => { isHoldingAttack = true; player.OnAttackPressed(); };
        inputActions.Player.Attack.canceled += ctx => { isHoldingAttack = false; player.OnAttackReleased(); };

        inputActions.Player.SpecialAttack.performed += ctx => player.SpecialAttack();

        inputActions.Enable();
    }

    private void Update()
    {
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