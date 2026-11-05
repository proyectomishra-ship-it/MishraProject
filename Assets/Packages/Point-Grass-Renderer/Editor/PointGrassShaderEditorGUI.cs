using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public class PointGrassShaderEditorGUI : ShaderGUI {
    private Material target;
    private MaterialEditor editor;
    private MaterialProperty[] properties;

    private static GUIContent staticLabel = new GUIContent();

    private static GUIContent MakeLabel(string text, string tooltip = null) {
        staticLabel.text = text;
        staticLabel.tooltip = tooltip;
        return staticLabel;
    }

    private static GUIContent MakeLabel(MaterialProperty property, string tooltip = null) {
        staticLabel.text = property.displayName;
        staticLabel.tooltip = tooltip;
        return staticLabel;
    }

    public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties) {
        target = editor.target as Material;
        this.editor = editor;
        this.properties = properties;

        DrawMainProperties();
    }



    private MaterialProperty FindProperty(string name) => FindProperty(name, properties);

    private void RecordAction(string label) { editor.RegisterPropertyChangeUndo(label); }

    private void SetKeyword(string keyword, bool state) {
        if (state) { foreach (Material m in editor.targets) { m.EnableKeyword(keyword); } }
        else { foreach (Material m in editor.targets) { m.DisableKeyword(keyword); } }
    }

    private bool IsKeywordEnabled(string keyword) => target.IsKeywordEnabled(keyword);



    private void DrawMainProperties() {
        // Material Properties
            // Albedo Texture - _MainTex
                // Use Albedo Tex - BOOLEAN_ALBEDOTEX_ON
            // Smoothness - _Smoothness

        // Blade Colours
            // Top Col - _TopCol
            // Bottom Col - _BottomCol
            // Use Blade Col - BOOLEAN_BLADECOL_ON

        // Ground Colours
            // Ground Texture - _GroundTex
                // Use Ground Col - BOOLEAN_GROUNDCOL_ON

        // Mesh Transformation
            // Transformation Type - ENUM_TRANSFORM
            // Width - _Width
            // Height - _Height

        // Wind
            // Wind Strength - _WindStr

        // Normals
            // Use Normals - BOOLEAN_USEMESHNORMALS_ON

        DrawMaterialProperties();
        DrawBladeColourProperties();
        DrawGroundColourProperties();
        DrawMeshTransformationProperties(out MeshTransformationType transformType);
        DrawWindProperties();
        DrawNormalProperties(transformType == MeshTransformationType.TransformationMatrix);

        EditorGUILayout.Space();
        editor.RenderQueueField();
    }

    private void DrawMaterialProperties() {
        GUILayout.Label("Material Properties", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        // Albedo texture property
        MaterialProperty mainTex = FindProperty("_MainTex");
        EditorGUI.BeginChangeCheck();
        editor.TexturePropertySingleLine(MakeLabel("Albedo Texture", "The main albedo texture"), mainTex);
        if (EditorGUI.EndChangeCheck()) {
            RecordAction("Albedo Texture");
            // Set the keyword and parameter's float value
            bool enabled = mainTex.textureValue != null;
            FindProperty("BOOLEAN_ALBEDOTEX").floatValue = enabled ? 1f : 0f;
            SetKeyword("BOOLEAN_ALBEDOTEX_ON", enabled);
        }

        // Smoothness property
        MaterialProperty smoothness = FindProperty("_Smoothness");
        EditorGUI.BeginChangeCheck();
        editor.RangeProperty(smoothness, "Smoothness");
        if (EditorGUI.EndChangeCheck()) { RecordAction("Smoothness"); }

        EditorGUI.indentLevel--;
    }
    private void DrawBladeColourProperties() {
        GUILayout.Label("Blade Colour Properties", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        // Top colour
        MaterialProperty topCol = FindProperty("_TopCol");
        EditorGUI.BeginChangeCheck();
        editor.ColorProperty(topCol, "Top Colour");
        if (EditorGUI.EndChangeCheck()) { RecordAction("Top Colour"); }

        // Bottom colour
        MaterialProperty bottomCol = FindProperty("_BottomCol");
        EditorGUI.BeginChangeCheck();
        editor.ColorProperty(bottomCol, "Bottom Colour");
        if (EditorGUI.EndChangeCheck()) { RecordAction("Bottom Colour"); }

        // Top Brighten
        MaterialProperty topBright = FindProperty("_TopBright");
        EditorGUI.BeginChangeCheck();
        editor.FloatProperty(topBright, "Top Highlight Amount");
        if (EditorGUI.EndChangeCheck()) { RecordAction("Top Brighten"); }

        // Use blade colour keyword
        bool useBladeCol = IsKeywordEnabled("BOOLEAN_BLADECOL_ON");
        EditorGUI.BeginChangeCheck();
        useBladeCol = EditorGUILayout.ToggleLeft(MakeLabel("Use Blade Colours", "Enables the used of sampled blade colours (e.g. when using terrain distribution)"), useBladeCol);
        if (EditorGUI.EndChangeCheck()) {
            RecordAction("Use Blade Colour");
            // Set the keyword and parameter's float value
            FindProperty("BOOLEAN_BLADECOL").floatValue = useBladeCol ? 1f : 0f;
            SetKeyword("BOOLEAN_BLADECOL_ON", useBladeCol);
        }

        EditorGUI.indentLevel--;
    }
    private void DrawGroundColourProperties() {
        GUILayout.Label("Ground Colour Properties", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        // Ground Texture
        MaterialProperty groundTexture = FindProperty("_GroundTex");
        EditorGUI.BeginChangeCheck();
        editor.TexturePropertySingleLine(MakeLabel("Ground Texture", "The sampled ground texture"), groundTexture);
        if (EditorGUI.EndChangeCheck()) {
            RecordAction("Ground Texture");
            // Set the keyword and parameter's float value
            bool enabled = groundTexture.textureValue != null;
            FindProperty("BOOLEAN_GROUNDCOL").floatValue = enabled ? 1f : 0f;
            SetKeyword("BOOLEAN_GROUNDCOL_ON", enabled);
        }

        EditorGUI.indentLevel--;
    }
    private void DrawMeshTransformationProperties(out MeshTransformationType transformType) {
        GUILayout.Label("Mesh Transformation Properties", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        // Blade width
        MaterialProperty width = FindProperty("_Width");
        EditorGUI.BeginChangeCheck();
        editor.FloatProperty(width, "Width");
        if (EditorGUI.EndChangeCheck()) { RecordAction("Width"); }

        // Blade height
        MaterialProperty height = FindProperty("_Height");
        EditorGUI.BeginChangeCheck();
        editor.FloatProperty(height, "Height");
        if (EditorGUI.EndChangeCheck()) { RecordAction("Height"); }

        // Mesh Transformation
        transformType = MeshTransformationType.ViewSpaceOffset;
        if (IsKeywordEnabled("ENUM_TRANSFORM_TRANSFORM_MATRIX")) { transformType = MeshTransformationType.TransformationMatrix; }
        EditorGUI.BeginChangeCheck();
        transformType = (MeshTransformationType)EditorGUILayout.EnumPopup(MakeLabel("Mesh Transformation Type", "How should the blades\' vertices be transformed?" +
            "\n  View Space Offset - Always faces the camera" +
            "\n  Transformation Matrix - Mesh transformation independent from the camera"), transformType);
        if (EditorGUI.EndChangeCheck()) {
            RecordAction("Mesh Transformation Type");
            // Set the keyword and parameter's float value
            FindProperty("ENUM_TRANSFORM").floatValue = (float)transformType;
            SetKeyword("ENUM_TRANSFORM_VIEW_SPACE_OFFSET", transformType == MeshTransformationType.ViewSpaceOffset);
            SetKeyword("ENUM_TRANSFORM_TRANSFORM_MATRIX", transformType == MeshTransformationType.TransformationMatrix);
        }

        EditorGUI.indentLevel--;
    }
    private void DrawWindProperties() {
        GUILayout.Label("Wind Properties", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        // Wind strength
        MaterialProperty windStr = FindProperty("_WindStr");
        EditorGUI.BeginChangeCheck();
        editor.FloatProperty(windStr, "Wind Strength");
        if (EditorGUI.EndChangeCheck()) { RecordAction("Wind Strength"); }

        EditorGUI.indentLevel--;
    }
    private void DrawNormalProperties(bool meshTransformEnabled) {
        if (meshTransformEnabled) {
            GUILayout.Label("Normal Properties", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            // Use Mesh Normals
            bool useMeshNormals = IsKeywordEnabled("BOOLEAN_USEMESHNORMALS_ON");
            EditorGUI.BeginChangeCheck();
            useMeshNormals = EditorGUILayout.ToggleLeft(MakeLabel("Use Mesh Normals", "Should we use the mesh's normals instead of the sampled normal?"), useMeshNormals);
            if (EditorGUI.EndChangeCheck()) {
                RecordAction("Use Mesh Normals");
                // Set the keyword and parameter's float value
                FindProperty("BOOLEAN_USEMESHNORMALS").floatValue = useMeshNormals ? 1f : 0f;
                SetKeyword("BOOLEAN_USEMESHNORMALS_ON", useMeshNormals);
            }

            EditorGUI.indentLevel--;
        }
    }

    private enum MeshTransformationType {
        ViewSpaceOffset, TransformationMatrix
    }
}