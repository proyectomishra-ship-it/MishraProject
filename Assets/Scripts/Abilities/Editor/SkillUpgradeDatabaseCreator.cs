using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú RPG > Skill Upgrades > Crear / actualizar base de mejoras.
/// Crea Assets/Resources/SkillUpgradeDatabase.asset (si no existe) para poder editar desde el Inspector:
/// cada cuántos niveles se da una mejora, el primer nivel, si se pausa la partida, y el valor,
/// los máximos y la probabilidad de cada mejora.
/// Si el asset ya existe, solo le agrega las mejoras nuevas que falten (no pisa tu balance).
/// </summary>
public static class SkillUpgradeDatabaseCreator
{
    private const string Folder = "Assets/Resources";
    private const string AssetPath = Folder + "/SkillUpgradeDatabase.asset";

    [MenuItem("RPG/Skill Upgrades/Crear o actualizar base de mejoras")]
    public static void CreateOrUpdate()
    {
        var db = AssetDatabase.LoadAssetAtPath<SkillUpgradeDatabase>(AssetPath);

        if (db == null)
        {
            if (!Directory.Exists(Folder))
                Directory.CreateDirectory(Folder);

            db = ScriptableObject.CreateInstance<SkillUpgradeDatabase>();
            db.ResetToDefaults();

            AssetDatabase.CreateAsset(db, AssetPath);
            Debug.Log("[SkillUpgrades] Creado " + AssetPath);
        }
        else
        {
            db.SyncWithDefaults();
            Debug.Log("[SkillUpgrades] Actualizado " + AssetPath + " (se agregaron las mejoras que faltaban).");
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = db;
        EditorGUIUtility.PingObject(db);
    }
}
