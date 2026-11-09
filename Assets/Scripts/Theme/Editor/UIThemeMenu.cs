#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class UIThemeMenu
{
    private const string Folder = "Assets/Resources";
    private const string AssetPath = Folder + "/UITheme.asset";

    [MenuItem("Mishra/UI/Create Default Theme")]
    public static void CreateDefaultTheme()
    {
        var existing = AssetDatabase.LoadAssetAtPath<UITheme>(AssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log("[UITheme] Ya existe: " + AssetPath);
            return;
        }

        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var theme = ScriptableObject.CreateInstance<UITheme>();
        AssetDatabase.CreateAsset(theme, AssetPath);
        AssetDatabase.SaveAssets();

        Selection.activeObject = theme;
        EditorGUIUtility.PingObject(theme);
        Debug.Log("[UITheme] Creado en " + AssetPath);
    }

    /// <summary>Avisa de Canvas sin CanvasScaler o con escala incorrecta (p. ej. NetworkMenu).</summary>
    [MenuItem("Mishra/UI/Check Canvas Scalers In Open Scene")]
    public static void CheckCanvasScalers()
    {
        int problems = 0;
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas) continue;

            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null)
            {
                problems++;
                Debug.LogWarning($"[UI] '{canvas.name}' no tiene CanvasScaler.", canvas);
            }
            else if (scaler.uiScaleMode != UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                problems++;
                Debug.LogWarning($"[UI] '{canvas.name}' no usa Scale With Screen Size.", canvas);
            }
        }
        Debug.Log(problems == 0 ? "[UI] Canvas OK." : $"[UI] {problems} problema(s) de escala.");
    }
}
#endif
