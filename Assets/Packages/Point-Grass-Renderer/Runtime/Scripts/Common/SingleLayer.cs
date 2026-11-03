using UnityEngine;

namespace MicahW.PointGrass {
    [System.Serializable]
    public struct SingleLayer {
        [SerializeField]
        private int m_LayerIndex;
        public int LayerIndex => m_LayerIndex;
        public int Mask => 1 << m_LayerIndex;

        public SingleLayer(int layerIndex) { m_LayerIndex = layerIndex; }

        public void Set(int _layerIndex) {
            if (_layerIndex > 0 && _layerIndex < 32) { m_LayerIndex = _layerIndex; }
        }
    }
}