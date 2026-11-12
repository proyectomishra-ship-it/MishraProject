using UnityEngine;

[CreateAssetMenu(
    fileName = "New Collect Item Objective",
    menuName = "RPG/Quests/Objectives/Collect Item"
)]
public class CollectItemObjectiveData : QuestObjectiveData
{
    [Header("Target")]
    [Tooltip("Item que hay que encontrar (recoger del suelo, drops de enemigos, cofres...). " +
             "Cuenta cada unidad recogida mientras la misión está activa. " +
             "Los items obtenidos por crafteo o recompensas no cuentan.")]
    [SerializeField] private ItemData item;

    public ItemData Item => item;
}
