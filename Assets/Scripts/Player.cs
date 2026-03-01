using UnityEngine;

public class Player : Character
{
    [SerializeField] private PlayerClassData classData;

    protected override void Awake()
    {
        stats = new PlayerStats(characterData, classData);
    }

    public void AddExp(int amount)
    {
        ((PlayerStats)stats).AddExperience(amount);
    }

    public override void Move(Vector3 direction) { }
    public override void Run(Vector3 direction) { }
    public override void Jump() { }
}