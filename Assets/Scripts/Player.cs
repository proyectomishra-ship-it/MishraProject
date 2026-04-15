using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class Player : Character
{
    [SerializeField] private PlayerClassData classData;

    private PlayerInputController inputController;

    protected override void Awake()
    {
        base.Awake();

        inputController = GetComponent<PlayerInputController>();

        if (inputController == null)
            Debug.LogError($"[Player] Falta PlayerInputController en el prefab de {gameObject.name}");

        Cursor.lockState = CursorLockMode.Locked;
        // Oculta el cursor
        Cursor.visible = false;
    }

    public override void OnNetworkSpawn()
    {
      
        if (IsOwner)
            inputController?.Initialize(this);
    }

    protected override CharacterStats CreateStats()
    {
        return new PlayerStats(characterData, classData);
    }


    public void AddExp(int amount)
    {
        if (!IsServer) return;
        ((PlayerStats)stats).AddExperience(amount);
    }

    #region Movement - Owner → ServerRpc → Server



    public override void Move(Vector3 direction)
    {
        if (IsOwner) MoveServerRpc(direction);
    }

    public override void Run(Vector3 direction)
    {
        if (IsOwner) RunServerRpc(direction);
    }

    public override void Jump()
    {
        if (IsOwner) JumpServerRpc();
    }

    public override void ApplyGravity()
    {
        if (IsOwner) ApplyGravityServerRpc();
    }

    [ServerRpc]
    private void MoveServerRpc(Vector3 direction) => base.Move(direction);

    [ServerRpc]
    private void RunServerRpc(Vector3 direction) => base.Run(direction);

    [ServerRpc]
    private void JumpServerRpc() => base.Jump();

    [ServerRpc]
    private void ApplyGravityServerRpc() => base.ApplyGravity();

    #endregion

    #region Combat - Owner → ServerRpc → Server



    public override void OnAttackPressed()
    {
        if (IsOwner) OnAttackPressedServerRpc();
    }

    public override void OnAttackHeld()
    {
        if (IsOwner) OnAttackHeldServerRpc();
    }

    public override void OnAttackReleased()
    {
        if (IsOwner) OnAttackReleasedServerRpc();
    }

    public override void SpecialAttack()
    {
        if (IsOwner) SpecialAttackServerRpc();
    }

    [ServerRpc]
    private void OnAttackPressedServerRpc() => base.OnAttackPressed();

    [ServerRpc]
    private void OnAttackHeldServerRpc() => base.OnAttackHeld();

    [ServerRpc]
    private void OnAttackReleasedServerRpc() => base.OnAttackReleased();

    [ServerRpc]
    private void SpecialAttackServerRpc() => base.SpecialAttack();

    #endregion
}