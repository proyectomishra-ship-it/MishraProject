using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro global de todas las CraftingRecipeData del juego.
/// El ID de red de cada receta es su índice en la lista allRecipes —
/// mismo patrón que ItemDatabase. Hace falta porque un ServerRpc no puede
/// recibir una referencia directa a un ScriptableObject, solo tipos simples.
///
/// ACCIÓN: archivo nuevo en Assets/Scripts/Data/
/// Crear el asset en: Assets > Create > RPG > Crafting Recipe Database
/// Asignar en el Inspector de NetworkBootstrap y llamar Initialize() al
/// arrancar (igual que ItemDatabase).
/// </summary>
[CreateAssetMenu(menuName = "RPG/Crafting Recipe Database")]
public class CraftingRecipeDatabase : ScriptableObject
{
    public static CraftingRecipeDatabase Instance { get; private set; }

    [Tooltip("Todas las recetas del juego en orden. El índice = ID de red.")]
    [SerializeField] private List<CraftingRecipeData> allRecipes = new();

    private Dictionary<int, CraftingRecipeData> byId;
    private Dictionary<CraftingRecipeData, int> toId;

    public void Initialize()
    {
        Instance = this;
        byId = new();
        toId = new();

        for (int i = 0; i < allRecipes.Count; i++)
        {
            if (allRecipes[i] == null)
            {
                Debug.LogWarning($"[CraftingRecipeDatabase] Slot {i} vacío.");
                continue;
            }
            byId[i] = allRecipes[i];
            toId[allRecipes[i]] = i;
        }

        Debug.Log($"[CraftingRecipeDatabase] {byId.Count} recetas registradas.");
    }

    public CraftingRecipeData Get(int id) => byId.TryGetValue(id, out var v) ? v : null;
    public int GetId(CraftingRecipeData data) => toId.TryGetValue(data, out var v) ? v : -1;
    public IReadOnlyList<CraftingRecipeData> GetAll() => allRecipes;
}
