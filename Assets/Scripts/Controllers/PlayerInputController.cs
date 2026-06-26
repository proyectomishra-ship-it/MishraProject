using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputController : NetworkBehaviour
{
    private Player player;
    private PlayerInputActions inputActions;
    private Vector2 moveInput;

    // Bloquea input de movimiento (ej: mientras el inventario esta abierto)
    public bool IsInputBlocked { get; set; } = false;
    private bool isSprinting;
    private bool isHoldingAttack;
    private Camera mainCamera;

    // ?? Rate limit ????????????????????????????????????????????????????????????
    // El cliente manda RPCs de movimiento a tasa fija (inputSendRate/seg)
    // en vez de cada frame. As� el servidor no acumula colas de RPCs y
    // aplica el movimiento a su propio ritmo en MovementController.Update().
    // ?????????????????????????????????????????????????????????????????????????
    [SerializeField] private float inputSendRate = 20f;
    private float _sendTimer = 0f;
    private bool _wasMoving = false; // para enviar Stop() solo una vez

    public void Initialize(Player player)
    {
        this.player = player;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        if (player == null) player = GetComponent<Player>();
        if (player == null) { Debug.LogError("[PlayerInputController] No se encontr� el Player."); return; }

        mainCamera = Camera.main;

        inputActions = new PlayerInputActions();
        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        inputActions.Player.Jump.performed += ctx => { if (!IsInputBlocked) player.Jump(); };
        inputActions.Player.Sprint.performed += ctx => isSprinting = true;
        inputActions.Player.Sprint.canceled += ctx => isSprinting = false;
        inputActions.Player.Attack.performed += ctx =>
        {
            if (IsInputBlocked) return;
            isHoldingAttack = true;
            player.OnAttackPressed();
        };
        inputActions.Player.Attack.canceled += ctx =>
        {
            // Siempre limpiamos el flag local (si no, queda "atascado" en true
            // si el inventario se abre justo mientras mantenías click).
            bool wasHolding = isHoldingAttack;
            isHoldingAttack = false;
            if (!IsInputBlocked && wasHolding) player.OnAttackReleased();
        };
        inputActions.Player.SpecialAttack.performed += ctx => { if (!IsInputBlocked) player.SpecialAttack(); };
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
        if (IsInputBlocked)
        {
            if (_wasMoving) { player.Stop(); _wasMoving = false; }
            return;
        }

        if (isHoldingAttack)
            player.OnAttackHeld();

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Sin input ? avisar al servidor UNA sola vez que nos detuvimos
        if (moveInput == Vector2.zero)
        {
            if (_wasMoving)
            {
                player.Stop();
                _wasMoving = false;
            }
            _sendTimer = 0f;
            return;
        }

        // Con input ? mandar RPC a tasa fija, no cada frame
        _sendTimer += Time.deltaTime;
        float interval = 1f / Mathf.Max(inputSendRate, 1f);

        if (_sendTimer < interval) return;
        _sendTimer = 0f;

        Vector3 worldDir = GetWorldDirection();
        Quaternion targetRot = Quaternion.LookRotation(worldDir);

        if (isSprinting) player.Run(worldDir, targetRot);
        else player.Move(worldDir, targetRot);

        _wasMoving = true;
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