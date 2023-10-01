// Touchable point clouds
// by Norbert Pape and Simon Speiser
// using point clouds imported with Keijiro's
// norbertpape111@gmail.com
// Extension of Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx

using UnityEngine;
using System.Collections.Generic;

namespace PcxTouchable
{
    /// A container class optimized for compute buffer.
    public sealed class PointCloudData : ScriptableObject
    {
        #region Public properties


        public float distToCamera = 3f;
        public int zone = 5;
        public int reducedPointCount = 10;
        public bool healing = false;

        public int pointCount
        {
            get { return _pointData.Length; }
        }

        public Vector3[] boundingBox
        {
            get { return _boundingBox; }
        }

        public float boundingSphereRadius
        {
            get { return _boundingSphereRadius; }
        }

        public float density
        {
            get { return _density; }
        }

        public bool inFrustum = false;

        public ComputeBuffer computeBuffer
        {
            get
            {
                if (_pointBuffer == null)
                {
                    _pointBuffer = new ComputeBuffer(pointCount, sizeof(float) * 4);
                    _pointBuffer.SetData(_pointData);
                }
                return _pointBuffer;
            }
        }

        public ComputeBuffer computeBufferOriginal
        {
            get
            {
                if (_originalAndStyleBuffer == null)
                {
                    _originalAndStyleBuffer = new ComputeBuffer(pointCount, sizeof(float) * 4);
                    _originalAndStyleBuffer.SetData(_originalAndStyleData);
                }
                return _originalAndStyleBuffer;
            }
        }

        #endregion

        #region ScriptableObject implementation

        ComputeBuffer _pointBuffer, _pointBufferNormals, _originalAndStyleBuffer, _velocitiesBuffer;

        public float _boundingSphereRadius;
        public Vector3[] _boundingBox;
        public Vector3 _localCenter;
        public float _density;

        void OnDisable()
        {
            if (_pointBuffer != null)
            {
                _pointBuffer.Release();
                _pointBuffer = null;
            }
            if (_originalAndStyleBuffer != null)
            {
                _originalAndStyleBuffer.Release();
                _originalAndStyleBuffer = null;
            }
        }

        #endregion

        #region Serialized data members

        [System.Serializable]
        public struct Point
        {
            public Vector3 position;
            public uint color;
        }

        [SerializeField] Point[] _pointData;
        [SerializeField] Point[] _originalAndStyleData;

        #endregion

        public void Initialize(List<Vector3> positions, List<uint> colors, Vector3[] bbox, uint style, Vector3 centr, float bRadius)
        {
            _pointData = new Point[positions.Count];
            _originalAndStyleData = new Point[pointCount];

            for (var i = 0; i < _pointData.Length; i++)
            {
                _pointData[i] = new Point {
                    position = positions[i],
                    color = colors[i]
                };
                _originalAndStyleData[i] = new Point {
                    position = positions[i],
                    color = style
                };
            }
            _boundingBox = bbox;
            _boundingSphereRadius = bRadius;
            _localCenter = centr;
            Vector3 whd = bbox[1] - bbox[0];
            _density = (float) positions.Count / (whd.x * whd.y * whd.z);
        }
    }
}
