using UnityEngine;

[CreateAssetMenu(
    fileName = "New Quest Reward",
    menuName = "RPG/Quests/Reward"
)]
public class QuestRewardData : ScriptableObject
{
    [Header("Reward Type")]
    public QuestRewardType rewardType;

    [Header("Amount")]
    [Min(1)]
    public int amount = 1;

    [Header("Item")]
    [Tooltip("Item granted when the reward type is Item.")]
    public ItemData item;
}

public enum QuestRewardType
{
    Experience,
    Gold,
    Item
}