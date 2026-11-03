using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace MicahW.PointGrass {
    using static PointGrassCommon;

    public class PointGrassDisplacementManager : MonoBehaviour {
        public static PointGrassDisplacementManager instance;

        private ComputeBuffer objectsBuffer;
        private List<PointGrassDisplacer> displacers;

        private static readonly int maxDisplacerCount = 32;
        private int DisplacerCount => Mathf.Min(displacers.Count, maxDisplacerCount);

        public delegate void DisplacementDelegate(PointGrassDisplacementManager manager);
        public static event DisplacementDelegate OnInitialize;

        private void Awake() {
            if (instance != null) { Destroy(this); }
            else {
                instance = this;

                objectsBuffer = new ComputeBuffer(maxDisplacerCount, ObjectData.stride);
                displacers = new List<PointGrassDisplacer>();

                OnInitialize?.Invoke(this);
            }
        }
        private void OnDisable() {
            if (instance == this) {
                instance = null;

                objectsBuffer.Release();
                displacers.Clear();
            }
        }

        private void LateUpdate() { UpdateBuffer(); }

        // Update the compute buffer with the displacers' data
        private void UpdateBuffer() {
            var objectData = displacers.Select(a => a.GetObjectData()).ToArray();
            objectsBuffer.SetData(objectData, 0, 0, DisplacerCount);
        }

        // Update functions to add and remove displacers from the internal list
        public void AddDisplacer(PointGrassDisplacer displacer) => displacers.Add(displacer);
        public void RemoveDisplacer(PointGrassDisplacer displacer) => displacers.Remove(displacer);
        
        // Public method to add the displacement buffer to the point grass shader
        public void UpdatePropertyBlock(ref MaterialPropertyBlock block) {
            // Set buffer data here
            block.SetBuffer(ID_ObjBuff, objectsBuffer);
            block.SetInt(ID_ObjCount, DisplacerCount);
        }
    }
}