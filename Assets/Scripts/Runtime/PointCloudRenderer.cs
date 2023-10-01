// Touchable point clouds
// by Norbert Pape and Simon Speiser
// norbertpape111@gmail.com
// using point clouds imported with Keijiro's
// Extension of Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx

using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Rendering;


/// <summary>
/// Renders point clouds. We added Culling and dynamic resolution adjustement (based on distance) to Keijiro's renderer.
/// In order to do so, we had to compute bounding boxes and culling radii for the point clouds.
/// </summary>
namespace PcxTouchable
{
    /// A renderer class that renders a point cloud contained by PointCloudData.
    [ExecuteInEditMode]
    public sealed class PointCloudRenderer : MonoBehaviour
    {
        #region Editable attributes

        [SerializeField] public PointCloudData _sourceData = null;
        [SerializeField] [Range(0.7f,1.5f)] float gammaIntensity = 1.1f;

        public PointCloudData sourceData {
            get { return _sourceData; }
            set { _sourceData = value; }
        }

        #endregion

        #region Internal resources

        [SerializeField, HideInInspector] Shader _pointShader = null;
        public float _cullingRadius;
        [SerializeField] public float maxDistance = 100f;
        [SerializeField] public bool leaf = false;
        [SerializeField] public bool noNormals = true;

        #endregion

        #region Private objects

        Material _pointMaterial;

        public bool culling = true, adjustResolution = true;
        #endregion

        #region MonoBehaviour implementation
        
        void Start()
        {
            _cullingRadius = - _sourceData._boundingSphereRadius * this.transform.localScale.x;  //this presupposes proportional scaling, i.e. localScale.x=y=z

        }

        void OnDisable()
        {
            if (_pointMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_pointMaterial);
                }
                else
                {
                    DestroyImmediate(_pointMaterial);
                }
            }

        }

        public bool IsInFieldOfVision( GameObject gameObj, float cullingRadius)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);

            for (int i = 4; i > -1; i--) {
                if (planes[i].GetDistanceToPoint(gameObj.transform.position) < cullingRadius) {
                    return false;
                }
            }
            return true;
        }

        float AngleFactor(float dist)
        {
            if (dist > 0f)
            {
                Vector3 cameraObjectDir = (transform.position - Camera.main.transform.position).normalized;
                float angleFactor = Vector3.Dot(cameraObjectDir, Camera.main.transform.forward) - 1;
                return Mathf.Clamp(1f - angleFactor * angleFactor, 0f, 1f);
            }
            else return 1f;
        }

        float DistFactor(float dist)
        {
            return Mathf.Atan(-dist * dist * 0.25f) * 2f / Mathf.PI + 1f;
        }


        void OnRenderObject()
        {
            if (_sourceData == null ) return;

            if (!culling || IsInFieldOfVision(gameObject, _cullingRadius))
            {
                if (_pointMaterial == null)
                {
                    _pointMaterial = new Material(_pointShader);

                    _pointMaterial.SetBuffer("_PointBuffer", _sourceData.computeBuffer);
                }
                
                _pointMaterial.SetFloat("_gammaIntensity", gammaIntensity);
                _pointMaterial.SetPass(0);
                _pointMaterial.SetMatrix("_Transform", transform.localToWorldMatrix);
                _pointMaterial.SetVector("_cameraForward", transform.InverseTransformVector(Camera.main.transform.position - transform.position).normalized);
                if (adjustResolution)
                {
                    float dist = (Camera.main.transform.position - this.transform.position).magnitude + _cullingRadius;
                    int v = (int)(_sourceData.pointCount * DistFactor(dist) * AngleFactor(dist));
                    Graphics.DrawProceduralNow(MeshTopology.Points, v, 1);
                }
                else
                {
                    Graphics.DrawProceduralNow(MeshTopology.Points, _sourceData.pointCount, 1);
                }
            }

        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.TRS(this.transform.position, this.transform.rotation, this.transform.lossyScale);
            Vector3 dim = _sourceData.boundingBox[1] - _sourceData.boundingBox[0];
            Gizmos.DrawWireCube(Vector3.zero, dim);
        }
        #endregion
    }

}
