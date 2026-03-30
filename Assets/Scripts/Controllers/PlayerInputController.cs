using UnityEngine;

public class PlayerInputController : MonoBehaviour
{
    private Player player;
    private PlayerInputActions inputActions;
    private Vector2 moveInput;

    public void Initialize(Player player)
    {
        this.player = player;

        inputActions = new PlayerInputActions();

        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        inputActions.Enable();
    }

    private void Update()
    {
        if (player == null) return;

        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);

        player.Move(move); 
    }
}