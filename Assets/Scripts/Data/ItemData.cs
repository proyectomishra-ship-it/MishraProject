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

    [Header("Consumible (solo ItemType.Consumable)")]
    [Tooltip("Vida que restaura al usarlo. 0 = no cura.")]
    [SerializeField, Min(0f)] private float healthRestore = 0f;
    [Tooltip("Maná que restaura al usarlo. 0 = no restaura.")]
    [SerializeField, Min(0f)] private float manaRestore = 0f;

    public float HealthRestore => healthRestore;
    public float ManaRestore => manaRestore;

    /// <summary>Consumible con al menos un efecto (se puede "Usar").</summary>
    public bool IsUsableConsumable =>
        itemType == ItemType.Consumable && (healthRestore > 0f || manaRestore > 0f);

    public string ItemName => itemName;
    public ItemType ItemType => itemType;
    public Sprite Icon => icon;
    public string Description => description;
    public bool Stackable => stackable;
    public int MaxStack => maxStack;
}