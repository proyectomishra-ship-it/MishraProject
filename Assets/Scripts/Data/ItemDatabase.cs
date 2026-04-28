using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro global de todos los ItemData del juego.
/// El ID de red de cada item es su índice en la lista allItems.
/// ACCIÓN: archivo nuevo en Assets/Scripts/Data/
/// Crear el asset en: Assets > Create > RPG > Item Database
/// Asignar en el Inspector de NetworkBootstrap y llamar Initialize() al arrancar.
/// </summary>
[CreateAssetMenu(menuName = "RPG/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public static ItemDatabase Instance { get; private set; }

    [Tooltip("Todos los ItemData del juego en orden. El índice = ID de red.")]
    [SerializeField] private List<ItemData> allItems = new();

    private Dictionary<int, ItemData>  byId;
    private Dictionary<ItemData, int>  toId;

    public void Initialize()
    {
        Instance = this;
        byId = new();
        toId = new();

        for (int i = 0; i < allItems.Count; i++)
        {
            if (allItems[i] == null)
            {
                Debug.LogWarning($"[ItemDatabase] Slot {i} vacío.");
                continue;
            }
            byId[i]           = allItems[i];
            toId[allItems[i]] = i;
        }

        Debug.Log($"[ItemDatabase] {byId.Count} items registrados.");
    }

    public ItemData Get(int id)          => byId.TryGetValue(id, out var v) ? v : null;
    public int      GetId(ItemData data) => toId.TryGetValue(data, out var v) ? v : -1;
}
