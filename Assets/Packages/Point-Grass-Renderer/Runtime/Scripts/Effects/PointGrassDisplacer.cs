using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace MicahW.PointGrass {
    using static PointGrassCommon;

    using PGDM = PointGrassDisplacementManager;

    public class PointGrassDisplacer : MonoBehaviour {
        public Vector3 localPosition = Vector3.zero;
        public float radius = 0.5f;
        public float strength = 1f;

        private void Reset() {
            localPosition = Vector3.zero;
            radius = 0.5f;
            strength = 1f;
        }

        private void OnEnable() {
            if (PGDM.instance != null) { PGDM.instance.AddDisplacer(this); } // If the component was added at runtime
            else { PGDM.OnInitialize += Initialize; }                        // If the component initialized before the manager
        }
        private void OnDisable() {
            if (PGDM.instance != null) { PGDM.instance.RemoveDisplacer(this); }
        }
        // Called by the displacement manager instance after it has been initialized
        private void Initialize(PGDM manager) {
            manager.AddDisplacer(this);
            PGDM.OnInitialize -= Initialize; // Unsubscribe from the event
        }

        public ObjectData GetObjectData() => new ObjectData(transform.TransformPoint(localPosition), radius, strength);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected() {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.TransformPoint(localPosition), radius);
        }
#endif
    }
}