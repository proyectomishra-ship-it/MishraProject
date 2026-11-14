using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú RPG > Passives > Create Default Class Passives.
/// Crea los 3 assets de pasivas y los asigna al PlayerClassData de cada clase
/// (el nombre del asset debe contener Warrior / Mage / Hunter). No pisa assets existentes.
/// </summary>
public static class DefaultPassivesGenerator
{
    [MenuItem("RPG/Passives/Create Default Class Passives")]
    public static void Generate()
    {
        string folder = FindOutputFolder();

        PassiveAbility warrior = GetOrCreate<WarriorAuraPassive>(folder, "WarriorAura");
        PassiveAbility mage = GetOrCreate<BigFireballPassive>(folder, "BigFireball");
        PassiveAbility hunter = GetOrCreate<ExplosiveArrowPassive>(folder, "ExplosiveArrow");

        int assigned = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:PlayerClassData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<PlayerClassData>(AssetDatabase.GUIDToAssetPath(guid));

            if (data == null)
                continue;

            string n = data.name.ToLowerInvariant();
            PassiveAbility passive = n.Contains("warrior") ? warrior : n.Contains("mage") ? mage : n.Contains("hunter") ? hunter : null;

            if (passive == null)
                continue;

            var so = new SerializedObject(data);
            so.FindProperty("passiveAbility").objectReferenceValue = passive;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
            assigned++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Passives] Creadas en '{folder}' y asignadas a {assigned} PlayerClassData.");

        if (assigned < 3)
            Debug.LogWarning("[Passives] No se asignaron las 3 clases automáticamente: asignalas a mano en " +
                             "'Passive Ability' de cada PlayerClassData.");
    }

    private static T GetOrCreate<T>(string folder, string name) where T : ScriptableObject
    {
        string path = $"{folder}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);

        if (existing != null)
            return existing;

        var created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    private static string FindOutputFolder()
    {
        string parent = "Assets";

        foreach (string guid in AssetDatabase.FindAssets("MageShockwave t:SpecialAbility"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            parent = path.Substring(0, path.LastIndexOf('/'));
            break;
        }

        string folder = parent + "/Passives";

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(parent, "Passives");

        return folder;
    }
}
