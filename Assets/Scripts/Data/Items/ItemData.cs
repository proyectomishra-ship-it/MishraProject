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
    [SerializeField] private string itemName;
    [SerializeField] private ItemType itemType;
    [SerializeField] private bool stackable = true;
    [SerializeField] private int maxStack = 99;

    public string ItemName => itemName;
    public ItemType ItemType => itemType;
    public bool Stackable => stackable;
    public int MaxStack => maxStack;
}