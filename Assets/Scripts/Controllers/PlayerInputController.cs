using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputController : NetworkBehaviour
{
    private Player player;
    private PlayerInputActions inputActions;
    private Vector2 moveInput;
    private bool isSprinting;
    private bool isHoldingAttack;
    private Camera mainCamera;

    public void Initialize(Player player)
    {
        this.player = player;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        if (player == null) player = GetComponent<Player>();
        if (player == null) { Debug.LogError("[PlayerInputController] No se encontró el Player."); return; }

        mainCamera = Camera.main;
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
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        inputActions?.Disable();
        inputActions?.Dispose();
    }

    private void Update()
    {
        if (!IsOwner || player == null || mainCamera == null) return;

        if (isHoldingAttack)
            player.OnAttackHeld();

        if (moveInput == Vector2.zero) return;

        // Calcular dirección y rotación en el CLIENTE donde la cámara es exacta
        Vector3 worldDir = GetWorldDirection();
        Quaternion targetRot = Quaternion.LookRotation(worldDir);

        if (isSprinting) player.Run(worldDir, targetRot);
        else player.Move(worldDir, targetRot);

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private Vector3 GetWorldDirection()
    {
        Vector3 camForward = mainCamera.transform.forward;
        Vector3 camRight = mainCamera.transform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 dir = camForward * moveInput.y + camRight * moveInput.x;
        dir.y = 0f;
        return dir.normalized;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!IsOwner) return;
        if (hasFocus) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }
}