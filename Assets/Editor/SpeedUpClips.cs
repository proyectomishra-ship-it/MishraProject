// Poner en una carpeta llamada "Editor" dentro de Assets (ej: Assets/Editor/SpeedUpClips.cs)
// Uso: menu Tools > Speed Up Clips. Selecciona el FBX (o clips sueltos) en el Project,
// elegi el multiplicador y toca "Crear clips". Genera .anim editables ya acelerados.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class SpeedUpClips : EditorWindow
{
    float speed = 1.6f;
    string filter = "";   // ej: "atack,jump" -> solo clips cuyo nombre contenga alguna de esas palabras

    [MenuItem("Tools/Speed Up Clips")]
    static void Open() { GetWindow<SpeedUpClips>("Speed Up Clips"); }

    void OnGUI()
    {
        speed = EditorGUILayout.Slider("Multiplicador", speed, 0.5f, 4f);
        filter = EditorGUILayout.TextField("Filtro (coma, vacio = todos)", filter);
        EditorGUILayout.HelpBox("Selecciona el FBX o clips en el Project. Se crean .anim nuevos en la carpeta del FBX, carpeta 'Fast'.", MessageType.Info);
        if (GUILayout.Button("Crear clips")) Run();
    }

    void Run()
    {
        var words = filter.Split(',').Select(w => w.Trim().ToLower()).Where(w => w.Length > 0).ToArray();
        int n = 0;
        foreach (var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
            {
                if (clip.name.StartsWith("__preview__")) continue;
                if (words.Length > 0 && !words.Any(w => clip.name.ToLower().Contains(w))) continue;
                Create(clip, path); n++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("SpeedUpClips: " + n + " clips creados.");
    }

    void Create(AnimationClip src, string srcPath)
    {
        string dir = Path.Combine(Path.GetDirectoryName(srcPath), "Fast").Replace("\\", "/");
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(srcPath).Replace("\\", "/"), "Fast");

        string clean = src.name.Trim().Replace(" ", "_").Replace("}", "").TrimEnd('_');
        var dst = new AnimationClip { frameRate = src.frameRate, wrapMode = src.wrapMode };
        EditorUtility.CopySerialized(src, dst);   // copia loop, bounds, etc.
        dst.ClearCurves();

        float k = 1f / speed;
        foreach (var b in AnimationUtility.GetCurveBindings(src))
        {
            var c = AnimationUtility.GetEditorCurve(src, b);
            var keys = c.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].time *= k;
                keys[i].inTangent *= speed;
                keys[i].outTangent *= speed;
            }
            c.keys = keys;
            AnimationUtility.SetEditorCurve(dst, b, c);
        }
        var evs = AnimationUtility.GetAnimationEvents(src);
        foreach (var e in evs) e.time *= k;
        AnimationUtility.SetAnimationEvents(dst, evs);

        var settings = AnimationUtility.GetAnimationClipSettings(src);
        settings.startTime *= k; settings.stopTime *= k;
        AnimationUtility.SetAnimationClipSettings(dst, settings);

        string outPath = AssetDatabase.GenerateUniqueAssetPath(dir + "/" + clean + ".anim");
        AssetDatabase.CreateAsset(dst, outPath);
    }
}
