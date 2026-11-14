#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AudioMenu
{
    private const string Folder = "Assets/Resources";
    private const string AssetPath = Folder + "/AudioConfig.asset";

    [MenuItem("Mishra/Audio/Create Audio Config")]
    public static void CreateAudioConfig()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AudioConfig>(AssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log("[Audio] Ya existe: " + AssetPath);
            return;
        }

        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var config = ScriptableObject.CreateInstance<AudioConfig>();
        AssetDatabase.CreateAsset(config, AssetPath);
        AssetDatabase.SaveAssets();

        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
        Debug.Log("[Audio] Creado en " + AssetPath);
    }
}
#endif
