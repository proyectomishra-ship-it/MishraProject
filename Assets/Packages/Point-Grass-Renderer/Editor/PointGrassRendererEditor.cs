using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;

namespace MicahW.PointGrass {
    using static PointGrassCommon;

    [CustomEditor(typeof(PointGrassRenderer))]
    public class PointGrassRendererEditor : Editor {
        PointGrassRenderer renderer;

        private bool showMultipleMeshes;

        private SerializedProperty prop_distSource, prop_terrain, prop_chunkCount, prop_baseMesh,
            prop_bladeType, prop_multiMesh, prop_multiMat, prop_grassBladeMesh, prop_material,
            prop_shadowMode, prop_renderLayer,
            prop_pointCount, prop_multiplyByArea, prop_pointLODFactor,
            prop_randomiseSeed, prop_seed,
            prop_overwriteNormalDirection, prop_forcedNormal,
            prop_useDensity, prop_densityCutoff,
            prop_useLength, prop_lengthMapping,
            prop_projectType, prop_projectMask,
            prop_boundingBoxOffset;
        // Array Properties
        private SerializedProperty prop_grassMeshes, prop_meshDensityValues, prop_materials, prop_terrainLayers, prop_sceneFilters;
        // Readonly parameters
        private static readonly float halfLineHeight = EditorGUIUtility.singleLineHeight * 0.5f;

        private void OnEnable() {
            prop_distSource = serializedObject.FindProperty("distSource");
            prop_terrain = serializedObject.FindProperty("terrain");
            prop_chunkCount = serializedObject.FindProperty("chunkCount");
            prop_baseMesh = serializedObject.FindProperty("baseMesh");
            // Blade rendering
            prop_bladeType = serializedObject.FindProperty("bladeType");
            prop_multiMesh = serializedObject.FindProperty("multipleMeshes");
            prop_multiMat = serializedObject.FindProperty("multipleMaterials");
            prop_grassBladeMesh = serializedObject.FindProperty("grassBladeMesh");
            prop_material = serializedObject.FindProperty("material");
            
            prop_shadowMode = serializedObject.FindProperty("shadowMode");
            prop_renderLayer = serializedObject.FindProperty("renderLayer");
            // Point count
            prop_pointCount = serializedObject.FindProperty("pointCount");
            prop_multiplyByArea = serializedObject.FindProperty("multiplyByArea");
            prop_pointLODFactor = serializedObject.FindProperty("pointLODFactor");
            // Random seed
            prop_randomiseSeed = serializedObject.FindProperty("randomiseSeed");
            prop_seed = serializedObject.FindProperty("seed");
            // Overwrite normals
            prop_overwriteNormalDirection = serializedObject.FindProperty("overwriteNormalDirection");
            prop_forcedNormal = serializedObject.FindProperty("forcedNormal");
            // Density inputs
            prop_useDensity = serializedObject.FindProperty("useDensity");
            prop_densityCutoff = serializedObject.FindProperty("densityCutoff");
            // Length inputs
            prop_useLength = serializedObject.FindProperty("useLength");
            prop_lengthMapping = serializedObject.FindProperty("lengthMapping");
            // Projection
            prop_projectType = serializedObject.FindProperty("projectType");
            prop_projectMask = serializedObject.FindProperty("projectMask");
            // Bounding Box
            prop_boundingBoxOffset = serializedObject.FindProperty("boundingBoxOffset");
            // Array Properties
            prop_grassMeshes = serializedObject.FindProperty("grassBladeMeshes");
            prop_meshDensityValues = serializedObject.FindProperty("meshDensityValues");
            prop_materials = serializedObject.FindProperty("materials");
            prop_terrainLayers = serializedObject.FindProperty("terrainLayers");
            prop_sceneFilters = serializedObject.FindProperty("sceneFilters");
        }

        public override void OnInspectorGUI() {
            renderer = (PointGrassRenderer)target;

            DrawDistributionParameters();
            DrawGrassParameters();
            DrawPointParameters();
            DrawProjectionParameters();
            DrawBoundingBoxParameters();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawInformation(renderer);
            EditorGUILayout.Space();

            // ---  Buttons ---
            EditorGUI.BeginDisabledGroup(!renderer.enabled);
            if (GUILayout.Button("Refresh Points")) {
                // Build the points
                renderer.BuildPoints();
                // Queue an update to the scene (So the grass is visible as soon as possible)
                EditorApplication.QueuePlayerLoopUpdate();
            }
            EditorGUI.EndDisabledGroup();
            if (!renderer.enabled) { EditorGUILayout.HelpBox("The component is currently disabled", MessageType.Info); }
        }

        private void DrawDistributionParameters() {
            // --- Distribution Source ---
            EditorGUILayout.PropertyField(prop_distSource, MakeLabel("Distribution Source", "The distribution source of the point grass"));
            DistributionSource distSource = (DistributionSource)prop_distSource.enumValueIndex;

            // --- Distribution Source Fields ---
            if (distSource == DistributionSource.TerrainData) {
                EditorGUILayout.PropertyField(prop_terrain, MakeLabel("Terrain Data", "The terrain data used for distribution"));

                EditorGUILayout.PropertyField(prop_terrainLayers, MakeLabel("Terrain Layers", "The layers that are used for distribution"));
                EditorGUILayout.PropertyField(prop_chunkCount, MakeLabel("Chunk Count"));

                if (prop_terrain.objectReferenceValue as TerrainData == null) { EditorGUILayout.HelpBox("No terrain data is assigned! No points will be generated", MessageType.Warning); }
                else if (prop_terrainLayers.arraySize == 0) { EditorGUILayout.HelpBox("No terrain layers are assigned! No points will be generated", MessageType.Warning); }
            }
            else if (distSource == DistributionSource.SceneFilters) {
                EditorGUILayout.Space();
                // Draw the array
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(prop_sceneFilters, MakeLabel("Scene Mesh Filters", "The mesh filters that are used to create"));
                EditorGUI.indentLevel--;
                // Draw buttons
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Get Filters from Children")) { GetFiltersFromChildren(); }
                if (GUILayout.Button("Get Filters from Immediate Children")) { GetFiltersFromImmediateChildren(); }
                EditorGUILayout.EndHorizontal();
                // Draw the help box for the mesh filters
                DrawMeshFilterHelpBox();
            }
            else {
                Mesh mesh = null;
                if (distSource == DistributionSource.Mesh) {
                    EditorGUILayout.PropertyField(prop_baseMesh, MakeLabel("Base Mesh", "The mesh used to distribute the points"));
                    mesh = prop_baseMesh.objectReferenceValue as Mesh;
                }
                else if (distSource == DistributionSource.MeshFilter) {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null) { mesh = filter.sharedMesh; }
                }

                if (mesh == null) { EditorGUILayout.HelpBox("No mesh is assigned! No points will be generated", MessageType.Warning); }
                else if (!mesh.isReadable) { EditorGUILayout.HelpBox("The mesh doesn't have \"read/write\" enabled! Make sure it is enabled in the import settings. Otherwise this renderer will fail in a build", MessageType.Warning); }
            }
        }
        private void DrawGrassParameters() {
            DrawMeshBladeSettings();

            // --- Shadow Mode ---
            EditorGUILayout.PropertyField(prop_shadowMode, MakeLabel("Shadow Mode"));

            // --- Render Layer ---
            EditorGUILayout.PropertyField(prop_renderLayer, MakeLabel("Render Layer"));
        }
        private void DrawMeshBladeSettings() {
            // --- Blade Mesh ---
            bool showMaterialField = true;
            // Draw the blade type property
            EditorGUILayout.PropertyField(prop_bladeType, MakeLabel("Blade Type", "The type of grass blade that will be rendered.\nCertain blade types require certain shaders. Make sure you're using the right one for your material"));
            if ((BladeType)prop_bladeType.enumValueIndex == BladeType.Mesh) {
                // Draw the property for enabling multiple meshes
                EditorGUILayout.PropertyField(prop_multiMesh, MakeLabel("Use Multiple Meshes", "Enables the use of multiple meshes.\n Note: Each mesh type uses a separate draw call"));
                // If we're using multiple meshes
                if (prop_multiMesh.boolValue) {
                    // Begin a change check
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(prop_multiMat, MakeLabel("Use Multiple Materials"));
                    // If this value just changed
                    if (EditorGUI.EndChangeCheck()) { ValidateMaterials(); }

                    // Disable the singular material property if we're showing multiple materials
                    if (prop_multiMat.boolValue) { showMaterialField = false; }

                    EditorGUILayout.Space(halfLineHeight);
                    showMultipleMeshes = EditorGUILayout.BeginFoldoutHeaderGroup(showMultipleMeshes, "Multiple Mesh Settings", null, ShowMultipleMaterialsContextMenu);
                    if (showMultipleMeshes) {
                        // Indent the inner elements
                        EditorGUI.indentLevel++;
                        // Draw the grass mesh count property
                        EditorGUI.BeginChangeCheck();
                        int grassMeshCount = Mathf.Max(1, EditorGUILayout.IntField(MakeLabel("Grass Mesh Count", "The number of grass blade meshes used for rendering"), prop_grassMeshes.arraySize));
                        if (EditorGUI.EndChangeCheck()) {
                            prop_grassMeshes.arraySize = grassMeshCount;
                            prop_meshDensityValues.arraySize = grassMeshCount;
                            prop_materials.arraySize = grassMeshCount;
                        }
                        // Draw each entry
                        for (int i = 0; i < prop_grassMeshes.arraySize; i++) { DrawMeshBladeEntry(i); }
                        // Unindent the elements
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();
                    EditorGUILayout.Space(halfLineHeight);
                }
                else { EditorGUILayout.PropertyField(prop_grassBladeMesh, MakeLabel("Blade Mesh", "The mesh that will be rendered for each blade")); }
            }

            // --- Material ---
            if (showMaterialField) { EditorGUILayout.PropertyField(prop_material, MakeLabel("Grass Material")); }
        }
        private void DrawMeshBladeEntry(int index) {
            int currentIndent = EditorGUI.indentLevel;

            #region Mesh & Density
            // Get the properties at index 'i'
            SerializedProperty _prop_mesh = prop_grassMeshes.GetArrayElementAtIndex(index);
            SerializedProperty _prop_density = prop_meshDensityValues.GetArrayElementAtIndex(index);
            // Create a label
            EditorGUILayout.LabelField($"Mesh {index}", EditorStyles.boldLabel);
            // Start the horizontal group
            EditorGUILayout.BeginHorizontal();
            // Draw the mesh property
            EditorGUILayout.PropertyField(_prop_mesh, GUIContent.none);
            // Clear the indent level to remove gaps
            EditorGUI.indentLevel = 0;
            // Draw the density property
            EditorGUILayout.PropertyField(_prop_density, GUIContent.none, GUILayout.Width(64));
            // Reset the indent level
            EditorGUI.indentLevel = currentIndent;
            // End the horizontal group
            EditorGUILayout.EndHorizontal();
            // Begin a horizontal group
            EditorGUILayout.BeginHorizontal();
            #endregion

            #region Materials & Reordering Buttons
            // If we're using multiple materials, draw the material property
            if (prop_multiMat.boolValue && prop_materials.arraySize > index) {
                SerializedProperty _prop_mat = prop_materials.GetArrayElementAtIndex(index);
                EditorGUILayout.PropertyField(_prop_mat, GUIContent.none);
            }
            // Otherwise, just draw a flexible space
            else { GUILayout.FlexibleSpace(); }
            // Get a control rect for drawing the two buttons
            Rect buttonRect = EditorGUILayout.GetControlRect(GUILayout.Width(64));
            // Draw the left button
            Rect leftButton = buttonRect;
            leftButton.width /= 2f;
            if (index > 0 && GUI.Button(leftButton, "↑")) { SwapMeshElements(index, index - 1); }
            // Draw the right button
            Rect rightButton = leftButton;
            rightButton.position += Vector2.right * leftButton.width;
            if (index < prop_grassMeshes.arraySize - 1 && GUI.Button(rightButton, "↓")) { SwapMeshElements(index, index + 1); }
            // End the horizontal group
            EditorGUILayout.EndHorizontal();
            #endregion
        }
        private void DrawPointParameters() {
            // --- Point Count ---
            EditorGUILayout.PropertyField(prop_pointCount, MakeLabel("Point Count", "The number of points distributed on the base mesh"));
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(prop_multiplyByArea, MakeLabel("Multiply by Area", "Multiply the number of points by the mesh's surface area"));
            EditorGUI.indentLevel--;
            EditorGUILayout.PropertyField(prop_pointLODFactor, MakeLabel("Point LOD Factor", "How many of the generated points should be drawn\n\n(WIP) Temporarily added to test the viability of LODs"));

            // --- Seed ---
            EditorGUILayout.PropertyField(prop_randomiseSeed, MakeLabel("Randomise Seed", "Whether we should use a random seed when distributing points"));
            if (!prop_randomiseSeed.boolValue) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(prop_seed, MakeLabel("Seed", "The random seed"));
                EditorGUI.indentLevel--;
            }

            // --- Normal Direction ---
            EditorGUILayout.PropertyField(prop_overwriteNormalDirection, MakeLabel("Overwrite Normal Direction", "Whether the normals of the distributed points should be overwritten by a constant vector"));
            if (prop_overwriteNormalDirection.boolValue) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(prop_forcedNormal, MakeLabel("Overwritten Normal"));
                EditorGUI.indentLevel--;
            }

            // --- Vertex Inputs ---
            EditorGUILayout.PropertyField(prop_useDensity, MakeLabel("Use Density Inputs", "Whether the renderer should use blade density data. \n - RED vertex colour channel on meshes \n - Matching terrain layers on terrain"));
            if (prop_useDensity.boolValue) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(prop_densityCutoff);
                if (prop_densityCutoff.floatValue >= 1f) { EditorGUILayout.HelpBox("The density cutoff is too high! Points may not be generated", MessageType.Warning); }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(prop_useLength, MakeLabel("Use Length Inputs", "Whether the renderer should use blade length data. \n - GREEN vertex colour channel on meshes \n - Matching terrain layers on terrain"));
            if (prop_useLength.boolValue) {
                EditorGUI.indentLevel++;
                // Min max slider
                Vector2 lengthMapping = prop_lengthMapping.vector2Value;
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.MinMaxSlider(MakeLabel("Length Mapping", "The remapped length values of the grass blades"), ref lengthMapping.x, ref lengthMapping.y, 0f, 1f);
                if (EditorGUI.EndChangeCheck()) { prop_lengthMapping.vector2Value = lengthMapping; }
                EditorGUI.indentLevel--;
            }
        }
        private void DrawProjectionParameters() {
            // --- Projection Mode ---
            EditorGUILayout.PropertyField(prop_projectType, MakeLabel("Projection Type", "How should the points be projected onto the world"));
            if ((ProjectionType)prop_projectType.enumValueIndex != ProjectionType.None) {
                // --- Projection Mask ---
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(prop_projectMask, MakeLabel("Projection Layer Mask", "What layers should be used for projection?"));
                EditorGUI.indentLevel--;
            }
        }
        private void DrawBoundingBoxParameters() {
            // --- Bounding Box Offset ---
            EditorGUILayout.PropertyField(prop_boundingBoxOffset, MakeLabel("Bounding Box Offset", "The amount of offset applied to the transformed bounding box"));
        }
        private void DrawInformation(PointGrassRenderer renderer) {
            PointGrassRenderer.DebugInformation info = renderer.GetDebugInfo();
            // Calculate the size of the points in memory
            long sizeInBytes = info.totalPointCount;
            sizeInBytes *= MeshPoint.stride;
            string memorySize = FindSizeSuffix(sizeInBytes, 2);

            // Create the message string
            string message = $"Total point count: {info.totalPointCount}\n" +
                $"Expected size in memory: {memorySize}\n\n" +
                $"Has multiple buffers: {info.usingMultipleBuffers}";
            if (info.usingMultipleBuffers) {
                message += $"\n   Buffer count: {info.bufferCount}\n" +
                    $"   Smallest buffer: {info.smallestBuffer}\n" +
                    $"   Largest buffer: {info.largestBuffer}";
            }

            EditorGUILayout.HelpBox(message, MessageType.None);
        }

        private void ShowMultipleMaterialsContextMenu(Rect position) {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Reset Density Values"), false, OnResetDensityValues);
            menu.DropDown(position);
        }

        private void GetFiltersFromChildren() {
            // Get the filters
            MeshFilter[] filters = renderer.transform.GetComponentsInChildren<MeshFilter>(false);
            // Fill the array
            prop_sceneFilters.ClearArray();
            prop_sceneFilters.arraySize = filters.Length;
            for (int i = 0; i < filters.Length; i++) {
                SerializedProperty prop = prop_sceneFilters.GetArrayElementAtIndex(i);
                prop.objectReferenceValue = filters[i];
            }
        }
        private void GetFiltersFromImmediateChildren() {
            // Get the filters
            List<MeshFilter> filters = new List<MeshFilter>();
            foreach (Transform trans in renderer.transform) {
                if (trans.gameObject.activeInHierarchy) {
                    MeshFilter[] newFilters = trans.gameObject.GetComponents<MeshFilter>();
                    filters.AddRange(newFilters);
                }
            }
            // Fill the array
            prop_sceneFilters.ClearArray();
            prop_sceneFilters.arraySize = filters.Count;
            for (int i = 0; i < filters.Count; i++) {
                SerializedProperty prop = prop_sceneFilters.GetArrayElementAtIndex(i);
                prop.objectReferenceValue = filters[i];
            }
        }
        private void ValidateMaterials() {
            Object singleMat = prop_material.objectReferenceValue;
            if (prop_multiMat.boolValue && singleMat != null && prop_materials.isArray) {
                // Iterate through all the elements in the materials array and replace empty values with the material property
                for (int i = 0; i < prop_materials.arraySize; i++) {
                    SerializedProperty prop = prop_materials.GetArrayElementAtIndex(i);
                    if (prop.objectReferenceValue == null) { prop.objectReferenceValue = singleMat; }
                }
            }
            else if (!prop_multiMat.boolValue && singleMat == null && prop_materials.isArray) {
                // Iterate through all the elements in the materials array and replace the material property with the first material found
                for (int i = 0; i < prop_materials.arraySize; i++) {
                    Object multiMat = prop_materials.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (multiMat != null) { prop_material.objectReferenceValue = multiMat; break; }
                }
            }
        }
        private void OnResetDensityValues() {
            for (int i = 0; i < prop_meshDensityValues.arraySize; i++) {
                prop_meshDensityValues.GetArrayElementAtIndex(i).floatValue = 1f;
            }
            serializedObject.ApplyModifiedProperties();
        }
        private void SwapMeshElements(int indexA, int indexB) {
            // Index range checks
            if (indexA < 0 || indexA > prop_grassMeshes.arraySize || indexA > prop_materials.arraySize || indexA > prop_meshDensityValues.arraySize) return;
            if (indexB < 0 || indexB > prop_grassMeshes.arraySize || indexB > prop_materials.arraySize || indexB > prop_meshDensityValues.arraySize) return;
            // Get the values at index A
            Object meshA = prop_grassMeshes.GetArrayElementAtIndex(indexA).objectReferenceValue;
            Object matA = prop_materials.GetArrayElementAtIndex(indexA).objectReferenceValue;
            float densityA = prop_meshDensityValues.GetArrayElementAtIndex(indexA).floatValue;
            // Swap B into A
            prop_grassMeshes.GetArrayElementAtIndex(indexA).objectReferenceValue = prop_grassMeshes.GetArrayElementAtIndex(indexB).objectReferenceValue;
            prop_materials.GetArrayElementAtIndex(indexA).objectReferenceValue = prop_materials.GetArrayElementAtIndex(indexB).objectReferenceValue;
            prop_meshDensityValues.GetArrayElementAtIndex(indexA).floatValue = prop_meshDensityValues.GetArrayElementAtIndex(indexB).floatValue;
            // Swap A copies into B
            prop_grassMeshes.GetArrayElementAtIndex(indexB).objectReferenceValue = meshA;
            prop_materials.GetArrayElementAtIndex(indexB).objectReferenceValue = matA;
            prop_meshDensityValues.GetArrayElementAtIndex(indexB).floatValue = densityA;
        }

        private void DrawMeshFilterHelpBox() {
            int filterCount = 0;
            int validCount = 0;
            for (int i = 0; i < prop_sceneFilters.arraySize; i++) {
                SerializedProperty prop = prop_sceneFilters.GetArrayElementAtIndex(i);
                MeshFilter filter = prop.objectReferenceValue as MeshFilter;
                if (filter != null) {
                    filterCount++;
                    if (filter.sharedMesh != null && filter.sharedMesh.isReadable) { validCount++; }
                }
            }
            // Draw the help box
            if (filterCount != validCount) {
                bool plural = filterCount != 1;
                EditorGUILayout.HelpBox($"There {(plural ? "are" : "is")} {filterCount} mesh filter{(plural ? "s" : "")} referenced, but {(plural ? $"{filterCount - validCount} of them have" : "it has")} an invalid mesh assigned.\nCheck the following:" +
                    $"\n - The mesh filter{(plural ? "s have" : " has")} a mesh assigned\n - The assigned mesh{(plural ? "es have" : " has")} read/write enabled", MessageType.Warning);
            }
        }

        private GUIContent MakeLabel(string label, string tooltip = null) => new GUIContent(label, tooltip);

        private static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        private static string FindSizeSuffix(long value, int decimalPlaces = 1) {
            if (decimalPlaces < 0) { throw new System.ArgumentOutOfRangeException("decimalPlaces"); }
            if (value < 0) { return "-" + FindSizeSuffix(-value, decimalPlaces); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)System.Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag)
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            // Make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (System.Math.Round(adjustedSize, decimalPlaces) >= 1000) {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }
    }
}