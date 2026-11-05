using System.Collections;
using System.Collections.Generic;

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MicahW.PointGrass {
    [ExecuteAlways]
    public class PointGrassWind : MonoBehaviour {
        [Tooltip("The scale of the sampled noise")] public float windScale;
        [Tooltip("The range of the sampled noise")] public Vector2 noiseRange;
        [Space()]
        [Tooltip("The direction the wind will push the grass")] public Vector3 windDirection;
        [Tooltip("The distance the sampled noise moves each second")] public Vector3 windScroll;

        private Vector3 currentNoisePosition;

        private static int ID_vecA, ID_vecB, ID_valA;

#if UNITY_EDITOR
        private double previousEditorTime = 0f;

        private void OnValidate() => RefreshValues();
#endif

        private void OnEnable() {
            // Get the shader's property IDs
            GetShaderIDs();
            // Reset the current noise position
            currentNoisePosition = Vector3.zero;
            // Refresh the point grass wind parameters
            RefreshValues();

#if UNITY_EDITOR
            if (!Application.isPlaying) {
                previousEditorTime = 0f;
                EditorApplication.update += EditorUpdate;
            }
#endif
        }

#if UNITY_EDITOR
        private void OnDisable() {
            if (!Application.isPlaying) {
                EditorApplication.update -= EditorUpdate;
            }
        }

        private void EditorUpdate() {
            // Calculate the editor's timestep
            float timeStep = (float)(EditorApplication.timeSinceStartup - previousEditorTime);
            previousEditorTime = EditorApplication.timeSinceStartup;
            // Update the current noise position
            currentNoisePosition += windScroll * timeStep;
            // Refresh the shader values
            RefreshValues();
        }
#endif

        private void LateUpdate() {
#if UNITY_EDITOR
            // If we're in the editor and the application isn't playing, return to prevent updates since we're already updating when the editor updates
            if (!Application.isPlaying) { return; }
#endif
            // Update the wind's noise position
            currentNoisePosition += windScroll * Time.deltaTime;
            // Refresh the point grass wind parameters
            RefreshValues();
        }

        private static void GetShaderIDs() {
            ID_vecA = Shader.PropertyToID("_PG_VectorA");
            ID_vecB = Shader.PropertyToID("_PG_VectorB");
            ID_valA = Shader.PropertyToID("_PG_ValueA");
        }

        public void RefreshValues() {
            PackedProperties properties = PackProperties();

            Shader.SetGlobalVector(ID_vecA, properties.vecA);
            Shader.SetGlobalVector(ID_vecB, properties.vecB);
            Shader.SetGlobalFloat(ID_valA, properties.valA);
        }
        private PackedProperties PackProperties() => new PackedProperties(windDirection, currentNoisePosition, noiseRange, windScale);

        private struct PackedProperties {
            public Vector4 vecA;
            public Vector4 vecB;
            public float valA;

            public PackedProperties(Vector3 windDirection, Vector3 windScroll, Vector2 noiseRange, float windScale) {
                Vector4 vecA = windDirection;
                vecA.w = noiseRange.x;
                Vector4 vecB = windScroll;
                vecB.w = noiseRange.y;

                this.vecA = vecA;
                this.vecB = vecB;
                valA = windScale;
            }
        }
    }
}