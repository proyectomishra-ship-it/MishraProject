// Crea copias aceleradas (o ralentizadas) de animaciones, sin tocar las originales.
//
// Uso: menu Tools > Speed Up Clips
//   1. En la ventana Project selecciona el FBX (o uno o varios clips dentro del FBX, o archivos .anim).
//   2. En esta ventana tilda SOLO las animaciones que queres acelerar.
//   3. Elegi el multiplicador y toca "Crear".
//
// Las copias se guardan en una subcarpeta ("Fast") junto al archivo original, y su nombre lleva un
// sufijo (por defecto "_x1.6") para distinguirlas del original en el Animator y en los selectores.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public class SpeedUpClips : EditorWindow
{
    private class Entry
    {
        public AnimationClip clip;
        public string sourcePath;
        public string key;          // sourcePath + "::" + nombre del clip
    }

    [SerializeField] private float speed = 1.6f;
    [SerializeField] private string suffix = "_x{x}";
    [SerializeField] private string folderName = "Fast";
    [SerializeField] private bool overwrite = true;
    [SerializeField] private bool lockList;
    [SerializeField] private string filter = "";
    [SerializeField] private List<string> checkedKeys = new List<string>();

    private readonly List<Entry> entries = new List<Entry>();
    private Vector2 scroll;
    private GUIStyle grayMini;

    [MenuItem("Tools/Speed Up Clips")]
    private static void Open()
    {
        var w = GetWindow<SpeedUpClips>("Speed Up Clips");
        w.minSize = new Vector2(460f, 520f);
        w.Rebuild();
    }

    private void OnEnable() => Rebuild();

    private void OnSelectionChange()
    {
        if (lockList) return;
        Rebuild();
        Repaint();
    }

    // =========================================================
    // LISTA DE ANIMACIONES
    // =========================================================

    private void Rebuild()
    {
        entries.Clear();
        var seen = new HashSet<string>();

        foreach (var obj in Selection.objects)
        {
            if (obj == null) continue;

            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) continue;

            if (obj is AnimationClip direct)
            {
                // Clip elegido a mano: solo ese, y ya tildado.
                if (AddEntry(direct, path, seen) && !checkedKeys.Contains(KeyOf(path, direct)))
                    checkedKeys.Add(KeyOf(path, direct));
            }
            else
            {
                // FBX u otro archivo: se listan todos sus clips para elegir.
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                    AddEntry(clip, path, seen);
            }
        }

        entries.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.sourcePath, b.sourcePath);
            return c != 0 ? c : string.CompareOrdinal(a.clip.name, b.clip.name);
        });
    }

    private bool AddEntry(AnimationClip clip, string path, HashSet<string> seen)
    {
        if (clip == null || clip.name.StartsWith("__preview__")) return false;

        string key = KeyOf(path, clip);
        if (!seen.Add(key)) return false;

        entries.Add(new Entry { clip = clip, sourcePath = path, key = key });
        return true;
    }

    private static string KeyOf(string path, AnimationClip clip) => path + "::" + clip.name;

    private bool Matches(Entry e)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;

        // Coma = "o". Ej: "atack, jump" muestra los clips que contengan "atack" o "jump".
        foreach (var raw in filter.Split(','))
        {
            string t = raw.Trim();
            if (t.Length == 0) continue;
            if (e.clip.name.IndexOf(t, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    private void SetVisible(bool value)
    {
        foreach (var e in entries)
        {
            if (!Matches(e)) continue;
            if (value) { if (!checkedKeys.Contains(e.key)) checkedKeys.Add(e.key); }
            else checkedKeys.Remove(e.key);
        }
    }

    private void InvertVisible()
    {
        foreach (var e in entries)
        {
            if (!Matches(e)) continue;
            if (!checkedKeys.Remove(e.key)) checkedKeys.Add(e.key);
        }
    }

    // =========================================================
    // NOMBRES
    // =========================================================

    private static string CleanChars(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
            sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '.' ? c : '_');
        return sb.ToString();
    }

    private static string CleanBaseName(string name)
    {
        string s = CleanChars(name.Trim());
        while (s.Contains("__")) s = s.Replace("__", "_");
        s = s.Trim('_');
        return s.Length == 0 ? "Clip" : s;
    }

    private string OutputName(AnimationClip src)
    {
        string x = speed.ToString("0.##", CultureInfo.InvariantCulture);
        string suf = CleanChars((suffix ?? "").Replace("{x}", x));
        return CleanBaseName(src.name) + suf;
    }

    private string OutputFolder(string sourcePath)
    {
        string parent = Path.GetDirectoryName(sourcePath).Replace("\\", "/");
        string folder = CleanChars((folderName ?? "").Trim()).Trim('_');
        if (folder.Length == 0) folder = "Fast";
        return parent + "/" + folder;
    }

    // =========================================================
    // INTERFAZ
    // =========================================================

    private void OnGUI()
    {
        if (grayMini == null)
        {
            grayMini = new GUIStyle(EditorStyles.miniLabel);
            grayMini.normal.textColor = Color.gray;
        }

        EditorGUILayout.HelpBox(
            "En la ventana Project selecciona el FBX (o uno o varios clips dentro del FBX). " +
            "Abajo aparece la lista: tilda solo las animaciones que queres acelerar.",
            MessageType.Info);

        DrawSettings();
        EditorGUILayout.Space(6);
        DrawList();
        DrawFooter();
    }

    private void DrawSettings()
    {
        EditorGUILayout.LabelField("1. Velocidad", EditorStyles.boldLabel);
        speed = EditorGUILayout.Slider(
            new GUIContent("Multiplicador", "1 = igual. 2 = el doble de rapido. 0.5 = a la mitad de velocidad."),
            speed, 0.25f, 4f);

        if (Mathf.Approximately(speed, 1f))
            EditorGUILayout.HelpBox("Con 1 la copia queda igual al original.", MessageType.Warning);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("2. Nombre de las copias", EditorStyles.boldLabel);
        suffix = EditorGUILayout.TextField(
            new GUIContent("Sufijo", "Se agrega al final del nombre del original. {x} se reemplaza por el multiplicador."),
            suffix);
        folderName = EditorGUILayout.TextField(
            new GUIContent("Carpeta de salida", "Subcarpeta, junto al archivo original, donde se guardan las copias."),
            folderName);
        overwrite = EditorGUILayout.ToggleLeft(
            "Si la copia ya existe, actualizarla (el Animator conserva la referencia)", overwrite);

        if (string.IsNullOrWhiteSpace(suffix))
            EditorGUILayout.HelpBox(
                "Sin sufijo, la copia se llama igual que el original y es dificil distinguirla.",
                MessageType.Warning);
    }

    private void DrawList()
    {
        EditorGUILayout.LabelField("3. Elegi las animaciones", EditorStyles.boldLabel);

        if (entries.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No hay animaciones para mostrar. Selecciona un FBX (o clips) en la ventana Project.",
                MessageType.None);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            filter = EditorGUILayout.TextField(
                new GUIContent("Buscar", "Filtra la lista por nombre. Separa con coma para buscar varias palabras: atack, jump"),
                filter);
            lockList = GUILayout.Toggle(
                lockList,
                new GUIContent("Bloquear lista", "Mantiene esta lista aunque selecciones otras cosas en el Project."),
                "Button", GUILayout.Width(100));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Marcar las visibles")) SetVisible(true);
            if (GUILayout.Button("Desmarcar las visibles")) SetVisible(false);
            if (GUILayout.Button("Invertir")) InvertVisible();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(180f), GUILayout.ExpandHeight(true));

        string lastSource = null;
        foreach (var e in entries)
        {
            if (!Matches(e)) continue;

            if (e.sourcePath != lastSource)
            {
                lastSource = e.sourcePath;
                EditorGUILayout.LabelField(Path.GetFileName(e.sourcePath), EditorStyles.boldLabel);
            }

            DrawRow(e);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawRow(Entry e)
    {
        bool was = checkedKeys.Contains(e.key);
        bool now;

        using (new EditorGUILayout.HorizontalScope())
        {
            now = EditorGUILayout.ToggleLeft(new GUIContent(e.clip.name.Trim(), e.clip.name), was);
            GUILayout.Label(Seconds(e.clip.length) + "  >  " + Seconds(e.clip.length / speed),
                            grayMini, GUILayout.Width(120f));
        }

        if (now != was)
        {
            if (now) checkedKeys.Add(e.key);
            else checkedKeys.Remove(e.key);
        }

        if (now)
        {
            string path = OutputFolder(e.sourcePath) + "/" + OutputName(e.clip) + ".anim";
            string status = AssetDatabase.LoadAssetAtPath<AnimationClip>(path) == null
                ? ""
                : overwrite ? "  (ya existe: se actualiza)" : "  (ya existe: se crea una copia numerada)";

            EditorGUILayout.LabelField("      se crea como:  " + OutputName(e.clip) + status, grayMini);
        }
    }

    private void DrawFooter()
    {
        int n = entries.Count(e => checkedKeys.Contains(e.key));

        EditorGUILayout.Space(4);

        if (entries.Count > 0 && n == 0)
            EditorGUILayout.HelpBox("Tilda al menos una animacion de la lista.", MessageType.Info);

        using (new EditorGUI.DisabledScope(n == 0))
        {
            if (GUILayout.Button(n == 1 ? "Crear 1 clip" : "Crear " + n + " clips", GUILayout.Height(30f)))
                Run();
        }
    }

    private static string Seconds(float s) => s.ToString("0.00", CultureInfo.InvariantCulture) + "s";

    // =========================================================
    // CREACION DE LOS CLIPS
    // =========================================================

    private void Run()
    {
        var selected = entries.Where(e => checkedKeys.Contains(e.key)).ToList();
        if (selected.Count == 0) return;

        var used = new HashSet<string>();
        var made = new List<string>();
        string lastFolder = null;

        try
        {
            for (int i = 0; i < selected.Count; i++)
            {
                var e = selected[i];
                EditorUtility.DisplayProgressBar("Speed Up Clips", e.clip.name, (float)i / selected.Count);

                try
                {
                    string outPath = Create(e, used);
                    made.Add(outPath);
                    lastFolder = Path.GetDirectoryName(outPath).Replace("\\", "/");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[SpeedUpClips] No se pudo crear la copia de '" + e.clip.name + "': " + ex.Message);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SpeedUpClips] " + made.Count + " clip(s) creados (x" +
                  speed.ToString("0.##", CultureInfo.InvariantCulture) + "):\n - " + string.Join("\n - ", made));

        ShowNotification(new GUIContent(made.Count + " clip(s) creados"));

        if (lastFolder != null)
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(lastFolder));
    }

    private string Create(Entry entry, HashSet<string> used)
    {
        AnimationClip src = entry.clip;
        float k = 1f / speed;

        // ---- carpeta y nombre ----
        string parent = Path.GetDirectoryName(entry.sourcePath).Replace("\\", "/");
        string dir = OutputFolder(entry.sourcePath);
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder(parent, dir.Substring(parent.Length + 1));

        string outPath = dir + "/" + OutputName(src) + ".anim";
        bool reuse = false;

        if (used.Contains(outPath))
        {
            outPath = AssetDatabase.GenerateUniqueAssetPath(outPath);     // dos clips con el mismo nombre en esta tanda
        }
        else if (AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath) != null)
        {
            if (overwrite) reuse = true;
            else outPath = AssetDatabase.GenerateUniqueAssetPath(outPath);
        }
        used.Add(outPath);

        // ---- copia con el tiempo escalado ----
        var dst = new AnimationClip { frameRate = src.frameRate, wrapMode = src.wrapMode };
        EditorUtility.CopySerialized(src, dst);          // copia loop, bounds, etc.
        dst.ClearCurves();

        foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(dst))
            AnimationUtility.SetObjectReferenceCurve(dst, b, null);

        foreach (var b in AnimationUtility.GetCurveBindings(src))
        {
            var c = AnimationUtility.GetEditorCurve(src, b);
            if (c == null) continue;

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

        foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(src))
        {
            var frames = AnimationUtility.GetObjectReferenceCurve(src, b);
            for (int i = 0; i < frames.Length; i++)
                frames[i].time *= k;
            AnimationUtility.SetObjectReferenceCurve(dst, b, frames);
        }

        var evs = AnimationUtility.GetAnimationEvents(src);
        foreach (var ev in evs) ev.time *= k;
        AnimationUtility.SetAnimationEvents(dst, evs);

        var settings = AnimationUtility.GetAnimationClipSettings(src);
        settings.startTime *= k;
        settings.stopTime *= k;
        AnimationUtility.SetAnimationClipSettings(dst, settings);

        // El nombre del clip (el que se ve en el Animator) = nombre del archivo, con el sufijo.
        dst.name = Path.GetFileNameWithoutExtension(outPath);
        dst.hideFlags = HideFlags.None;

        // ---- guardar ----
        if (reuse)
        {
            // Se actualiza el asset existente: conserva su GUID y las referencias que ya lo usan.
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
            EditorUtility.CopySerialized(dst, existing);
            existing.name = dst.name;
            existing.hideFlags = HideFlags.None;
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(dst);
        }
        else
        {
            AssetDatabase.CreateAsset(dst, outPath);
        }

        return outPath;
    }
}