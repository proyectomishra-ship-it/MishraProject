#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Crea 3 mallas de "mata de pasto" (varias hojas curvas juntas) para usar con
/// Point Grass Renderer en Blade Type = Mesh.
/// Menu: Tools > Grass > Crear matas de pasto
/// Guarda las mallas en Assets/Art/GrassClumps/ (Mata_1, Mata_2, Mata_3).
///
/// Formato igual al de las hojas del paquete: X/Z = ancho (escalado por Width del material),
/// Y = altura de 0 a 1 (escalado por Height), UV.y = altura y alpha del color vertice = altura.
/// </summary>
public static class GrassClumpMeshCreator
{
    private const string Folder = "Assets/Art/GrassClumps";

    [MenuItem("Tools/Grass/Crear matas de pasto")]
    public static void CreateClumps()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art/GrassClumps"))
            AssetDatabase.CreateFolder("Assets/Art", "GrassClumps");

        // seed, hojas, altura minima, altura maxima
        CreateClump("Mata_1", 11, 9, 0.60f, 1.00f);
        CreateClump("Mata_2", 27, 7, 0.50f, 0.85f);
        CreateClump("Mata_3", 53, 11, 0.65f, 1.00f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Grass] Matas creadas en " + Folder);
    }

    private static void CreateClump(string name, int seed, int bladeCount, float minH, float maxH)
    {
        var rng = new System.Random(seed);
        float R() => (float)rng.NextDouble();

        // Cada hoja: 3 filas de 2 vertices + 1 punta = 7 vertices, 5 triangulos.
        float[] rows = { 0f, 0.45f, 0.8f };
        int vertsPerBlade = rows.Length * 2 + 1;

        var verts = new Vector3[bladeCount * vertsPerBlade];
        var uvs = new Vector2[verts.Length];
        var cols = new Color[verts.Length];
        var norms = new Vector3[verts.Length];
        var tris = new int[bladeCount * 5 * 3];

        int v = 0, t = 0;
        for (int b = 0; b < bladeCount; b++)
        {
            // Base en un circulo chico; la primera hoja va al centro y es la mas alta.
            float a = R() * Mathf.PI * 2f;
            float r = b == 0 ? 0f : (0.15f + R() * 0.45f);
            Vector3 baseP = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            Vector3 outDir = r > 0.001f ? baseP.normalized : new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));

            float h = b == 0 ? maxH : Mathf.Lerp(minH, maxH, R());
            float lean = (b == 0 ? 0.10f : 0.25f + R() * 0.35f);   // cuanto se curva hacia afuera
            float halfW = 0.16f + R() * 0.07f;                     // medio ancho de la hoja

            float phi = R() * Mathf.PI;                            // orientacion del ancho
            Vector3 wAxis = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));

            int start = v;

            for (int i = 0; i < rows.Length; i++)
            {
                float tt = rows[i];
                float taper = 1f - tt * tt * 0.65f;
                Vector3 center = baseP + outDir * (lean * tt * tt) + Vector3.up * (tt * h);
                Vector3 l = center - wAxis * halfW * taper;
                Vector3 rr = center + wAxis * halfW * taper;

                verts[v] = l;  uvs[v] = new Vector2(0f, tt); cols[v] = new Color(1f, 1f, 1f, tt * h); norms[v] = Vector3.up; v++;
                verts[v] = rr; uvs[v] = new Vector2(1f, tt); cols[v] = new Color(1f, 1f, 1f, tt * h); norms[v] = Vector3.up; v++;
            }
            // Punta
            verts[v] = baseP + outDir * lean + Vector3.up * h;
            uvs[v] = new Vector2(0.5f, 1f);
            cols[v] = new Color(1f, 1f, 1f, h);
            norms[v] = Vector3.up;
            v++;

            // Triangulos (2 tiras de quads + punta)
            for (int i = 0; i < rows.Length - 1; i++)
            {
                int a0 = start + i * 2;
                tris[t++] = a0;     tris[t++] = a0 + 1; tris[t++] = a0 + 3;
                tris[t++] = a0;     tris[t++] = a0 + 3; tris[t++] = a0 + 2;
            }
            int top = start + (rows.Length - 1) * 2;
            tris[t++] = top; tris[t++] = top + 1; tris[t++] = top + 2;
        }

        var mesh = new Mesh { name = name };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.colors = cols;
        mesh.normals = norms;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        string path = Path.Combine(Folder, name + ".asset").Replace("\\", "/");
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, path);
        }
    }
}
#endif
