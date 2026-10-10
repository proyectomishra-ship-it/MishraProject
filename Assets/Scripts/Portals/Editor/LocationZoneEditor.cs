using UnityEditor;
using UnityEngine;

/// <summary>
/// Agrega dos botones al inspector de LocationZone para no configurar la cueva a mano:
///   - "Agregar MeshColliders al modelo": el NavMesh global y el servidor necesitan
///     colliders en el suelo y las paredes. Agrega un MeshCollider a cada malla de
///     "Visuals" que no tenga uno. (Alternativa: tildar "Generate Colliders" en el
///     import del modelo de Blender.)
///   - "Ajustar radio de activación al modelo": calcula el radio para que la esfera
///     naranja cubra toda la cueva.
/// </summary>
[CustomEditor(typeof(LocationZone))]
public class LocationZoneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Herramientas del modelo", EditorStyles.boldLabel);

        if (GUILayout.Button("Agregar MeshColliders al modelo (si faltan)"))
            AddColliders((LocationZone)target);

        if (GUILayout.Button("Ajustar radio de activación al modelo"))
            FitRadius((LocationZone)target);

        EditorGUILayout.HelpBox(
            "Los MeshColliders de modelos muy detallados son caros. Para cuevas grandes conviene " +
            "una malla de colisión simplificada.", MessageType.None);
    }

    private static void AddColliders(LocationZone zone)
    {
        int added = 0;

        foreach (var root in zone.ToggledObjects)
        {
            if (root == null) continue;

            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (mf.GetComponent<Collider>() != null) continue;

                var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                mc.sharedMesh = mf.sharedMesh;
                added++;
            }
        }

        Debug.Log(added > 0
            ? $"[Portales] Se agregaron {added} MeshCollider(s) al modelo de '{zone.name}'."
            : $"[Portales] '{zone.name}': no faltaba ningún collider (o no hay mallas en los objetos de la zona).");
    }

    private static void FitRadius(LocationZone zone)
    {
        bool hasBounds = false;
        var bounds = new Bounds();

        foreach (var root in zone.ToggledObjects)
        {
            if (root == null) continue;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
        }

        if (!hasBounds)
        {
            Debug.LogWarning($"[Portales] '{zone.name}': no hay Renderers en los objetos de la zona. " +
                             "Primero poné el modelo dentro de 'Visuals'.", zone);
            return;
        }

        // Distancia del centro de la zona a la esquina más lejana del modelo, con margen.
        Vector3 center = zone.CenterPosition;
        float radius = 0f;

        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        for (int z = 0; z < 2; z++)
        {
            Vector3 corner = new Vector3(
                x == 0 ? bounds.min.x : bounds.max.x,
                y == 0 ? bounds.min.y : bounds.max.y,
                z == 0 ? bounds.min.z : bounds.max.z);

            radius = Mathf.Max(radius, Vector3.Distance(center, corner));
        }

        radius = Mathf.Ceil(radius + 5f);

        var so = new SerializedObject(zone);
        so.FindProperty("activationRadius").floatValue = radius;
        so.ApplyModifiedProperties();

        Debug.Log($"[Portales] '{zone.name}': radio de activación = {radius} m.");
    }
}
