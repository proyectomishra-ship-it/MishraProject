using UnityEngine;

public enum ItemType
{
    Consumable,
    Equipment,
    Material
}

[CreateAssetMenu(fileName = "ItemData", menuName = "RPG/Item")]
public class ItemData : ScriptableObject
{
    [Header("General")]
    [SerializeField] private string itemName;
    [SerializeField] private ItemType itemType;
    [SerializeField] private Sprite icon;
    [TextArea(2, 4)]
    [SerializeField] private string description;

    [Header("Stack")]
    [SerializeField] private bool stackable = true;
    [SerializeField] private int maxStack = 99;

    public string ItemName => itemName;
    public ItemType ItemType => itemType;
    public Sprite Icon => icon;
    public string Description => description;
    public bool Stackable => stackable;
    public int MaxStack => maxStack;
}