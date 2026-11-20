using UnityEngine;

[CreateAssetMenu(
    fileName = "New Craft Item Objective",
    menuName = "RPG/Quests/Objectives/Craft Item"
)]
public class CraftItemObjectiveData : QuestObjectiveData
{
    [Header("Target")]
    [Tooltip("Item que hay que craftear. Cuenta cada unidad producida " +
             "(si la receta entrega 2, un crafteo suma 2) mientras la misión está activa.")]
    [SerializeField] private ItemData item;

    public ItemData Item => item;

    [Header("Al completar")]
    [Tooltip("Si esta activo, al completarse la mision se retiran del inventario de cada jugador que "
             + "aporto, las unidades que se contaron para el objetivo.")]
    [SerializeField] private bool consumeOnComplete = true;

    public bool ConsumeOnComplete => consumeOnComplete;
}
