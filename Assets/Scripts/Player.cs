using UnityEngine;

public class Player : Character
{
    [SerializeField] private PlayerClassData classData;

    private PlayerInputController inputController;

    protected override void Awake()
    {
        base.Awake();

        inputController = GetComponent<PlayerInputController>();
        inputController ??= gameObject.AddComponent<PlayerInputController>();

        inputController.Initialize(this);
    }

    protected override CharacterStats CreateStats()
    {
        return new PlayerStats(characterData, classData);
    }

    public void AddExp(int amount)
    {
        ((PlayerStats)stats).AddExperience(amount);
    }

    public override void Move(Vector3 direction)
    {
        base.Move(direction);
    }

    public override void Run(Vector3 direction)
    {
        base.Run(direction);
    }

    public override void Jump()
    {
        base.Jump();
    }
}