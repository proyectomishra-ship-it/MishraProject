#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector y handles de MapBoundsKiller.
///
///  - Avisos en el Inspector: limites invertidos, ningun limite activo,
///    sin SafePoints, SafePoints fuera de los limites.
///  - Scene View: cada limite activo tiene un handle (cubo) que se arrastra
///    a lo largo de su eje. Con el objeto seleccionado.
/// </summary>
[CustomEditor(typeof(MapBoundsKiller))]
public class MapBoundsKillerEditor : Editor
{
    private readonly List<string> errors = new List<string>();

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var killer = (MapBoundsKiller)target;

        EditorGUILayout.Space();

        // ---- Errores de configuracion ----
        errors.Clear();
        killer.GetConfigErrors(errors);

        foreach (string error in errors)
            EditorGUILayout.HelpBox(error, MessageType.Error);

        // ---- Nada activado ----
        if (!killer.AnyLimitEnabled)
        {
            EditorGUILayout.HelpBox(
                "Todos los límites están desactivados: no mata a nadie. " +
                "Tildá el que quieras usar (los de altura son los principales).",
                MessageType.Info);
        }

        // ---- SafePoints ----
        SafePoint[] points = FindObjectsByType<SafePoint>(FindObjectsSortMode.None);

        if (points.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "No hay SafePoints en la escena. Sin ellos este script NO mata a nadie " +
                "(el jugador reviviría en el mismo lugar prohibido y moriría en bucle).",
                MessageType.Warning);
        }
        else
        {
            int reported = 0;

            foreach (SafePoint point in points)
            {
                if (!killer.IsOutside(point.Position, out MapBoundsKiller.Limit exceeded))
                    continue;

                if (reported++ >= 5)
                    break;

                EditorGUILayout.HelpBox(
                    $"El SafePoint '{point.name}' está FUERA de los límites " +
                    $"({exceeded.Label}). El jugador moriría de nuevo al revivir ahí.",
                    MessageType.Warning);
            }
        }
    }

    private void OnSceneGUI()
    {
        var killer = (MapBoundsKiller)target;

        Bounds area = killer.GetDrawBounds();
        MapBoundsKiller.Limit[] limits = killer.GetLimits();

        for (int i = 0; i < limits.Length; i++)
        {
            MapBoundsKiller.Limit limit = limits[i];

            if (!limit.Enabled)
                continue;

            // El handle vive en el centro del plano, sobre el eje del limite.
            Vector3 position = area.center;
            position = position - Vector3.Scale(position, limit.Axis) + limit.Axis * limit.Value;

            Handles.color = MapBoundsKiller.ColorFor(limit.Axis);
            float size = HandleUtility.GetHandleSize(position) * 0.12f;

            EditorGUI.BeginChangeCheck();

            Vector3 moved = Handles.Slider(
                position, limit.Axis, size, Handles.CubeHandleCap, 0f);

            if (EditorGUI.EndChangeCheck())
            {
                float value = Vector3.Dot(moved, limit.Axis);
                value = Mathf.Round(value * 100f) / 100f;

                serializedObject.Update();
                serializedObject.FindProperty(limit.Property).floatValue = value;
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
#endif
