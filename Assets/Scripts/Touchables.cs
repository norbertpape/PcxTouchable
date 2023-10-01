// Touchable point clouds
// by Norbert Pape and Simon Speiser
// using point clouds imported with Keijiro's
// norbertpape111@gmail.com
// Extension of Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx

using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System;
using Oculus.Interaction.Input;


/// Makes a point cloud object touchable on the level of points.
/// Further, points that have been moved by the fingers will slowly return to their original positions.
/// The work is done on the GPU with the ComputeShader (touchables.compute). Since this can nonetheless 
/// be computationally heavy, some tricks have been applied to reduce computation when unnecessary. 
/// 1) The computeshader will only be activated if one of the hands is close enough,
/// 2) or if the point cloud is 'healing'.

namespace PcxTouchable 
{ 
    public sealed class Touchables : MonoBehaviour
    {
        #region Editable attributes

        [Header("OVR hand data")]
        public bool debugMode = false; //If true, the Hand meshes can be moved manually in the editor (or with FlyCamera) to touch the point cloud.
        [SerializeField] public OVRHand leftHand, rightHand;


        [Header("Touchables")]
        [SerializeField] public GameObject touchablePointCloud = null;

        [Header("Compute shader")]
        [SerializeField] ComputeShader _touchablesShader = null;
        private ComputeBuffer healingProcess = null;

        [Header("Point movement parameters")]
        [SerializeField] [Range(0f, 0.01f)] float _healParameter = 0.001f;
        [SerializeField] [Range(0f, 0.3f)] public float _radiusHandSq = 0.0225f;


        #endregion

        #region Non-editable attributes

        //relavant to the touchable pointclouds
        private PointCloudData _sourceData = null;
        private int threadsize, kernel;
        private uint[] healingProcessCurrent = new uint[1] { 0 }; //has to be an array since we will use this to send data back from the GPU via a computebuffer.
        private uint healingProcessLast;
        private bool pointsInMovement = false; // For the sake of efficiency, this tells if points are active.


        //relevant to the hands
        private Transform[] _handBones = null; //first 26 for one hand, then next 26 for the other
        private Transform[] fingertips = null;
        private Vector4[] handBones;
        private int bonesCount;
        private bool handNotCalibrated = true;

        //Scale of the respective handbones. Values extracted from Oculus Interaction package
        //It is wasteful to use a Vector4 array instead of a simple float array, 
        //but I have not succeeded in sending float arrays to the GPU using ComputeShader.SetFloats
        // https://forum.unity.com/threads/compute-shader-setfloats-broken.804585/
        //Let me know if you know how to get around this issue!

        private Vector4[] boneRadii = new Vector4[] { 
                new Vector4(0.01029526f,0f,0f,0f), 
                new Vector4(0.008038101f,0f,0f,0f), 
                new Vector4(0.007636196f,0f,0f,0f), 
                Vector4.zero, 
                new Vector4(0.01117394f,0f,0f,0f), 
                new Vector4(0.008030958f,0f,0f,0f), 
                new Vector4(0.00762941f,0f,0f,0f), 
                Vector4.zero, 
                new Vector4(0.009922139f,0f,0f,0f), 
                new Vector4(0.007611674f,0f,0f,0f), 
                new Vector4(0.00723109f,0f,0f,0f), 
                Vector4.zero, 
                new Vector4(0.008483353f,0f,0f,0f), 
                new Vector4(0.006764191f,0f,0f,0f), 
                new Vector4(0.006425982f,0f,0f,0f), 
                Vector4.zero, 
                new Vector4(0.01028296f, 0f, 0f, 0f), 
                new Vector4(0.009768807f, 0f, 0f, 0f),
                new Vector4(0.02323196f, 0f, 0f, 0f),
                new Vector4(0.01608828f, 0f, 0f, 0f),
                new Vector4(0.02346085f, 0f, 0f, 0f),
                new Vector4(0.01822828f, 0f, 0f, 0f),
                new Vector4(0.01822828f, 0f, 0f, 0f)
            };

    #endregion

    #region MonoBehaviour implementation



    /// <summary>
    /// Finds the transforms of the articulations fo the hand, chooses particular ones. It will 
    /// be sent to the GPU in order to construct capsule and prism 'colliders' roughly enveloping
    /// the hand.
    /// </summary>
    /// <returns> An array of ordered joint transforms. </returns>
    Transform[] InitializeHandBones()
        {
            List<Transform> handBones = new List<Transform>();
            List<Transform> tips = new List<Transform>();
            string[] fingers = { "index", "middle", "ring", "pinky", "thumb" };
            string[] hands = { "l", "r" };

            foreach (var hand in hands) {
                foreach (var finger in fingers)
                {
                    tips.Add(GameObject.Find(hand + "_" + finger + "_finger_tip_marker").transform);
                    for (int i = 1; i < 4; i++) {
                        if (finger == "thumb" && i == 1) i++;
                        handBones.Add(GameObject.Find("b_" + hand + "_" + finger + i.ToString()).transform);
                    }
                    handBones.Add(GameObject.Find("b_" + hand + "_" + finger + "_null").transform);
                }
                handBones.Add(GameObject.Find("b_" + hand + "_wrist").transform);
                handBones.Add(GameObject.Find("b_" + hand + "_thumb2").transform);
                handBones.Add(GameObject.Find("b_" + hand + "_index1").transform);
                handBones.Add(GameObject.Find("b_" + hand + "_middle1").transform);
                handBones.Add(GameObject.Find("b_" + hand + "_ring1").transform);
                handBones.Add(GameObject.Find("b_" + hand + "_pinky1").transform);
                handBones.Add(GameObject.Find("b_" + hand + "_pinky0").transform);
            }
            fingertips = tips.ToArray();
            return handBones.ToArray();
        }

        /// <summary>
        /// Copies the position of the articulations of the hands into an array of Vector4
        /// to be sent to the GPU.
        /// </summary>
        void UpdateHandBones()
        {
            for (int i = 0; i < handBones.Length; i++)
            {
                handBones[i] = _handBones[i].position;
            }
        }


        void Start()
        {
            _handBones = InitializeHandBones();

            bonesCount = _handBones.Length;

            handBones = new Vector4[bonesCount];
            UpdateHandBones();

            _sourceData = new PointCloudData();

            healingProcess = new ComputeBuffer(1, sizeof(uint));
            healingProcess.SetData(healingProcessCurrent);
                
            _sourceData = touchablePointCloud.GetComponent<PointCloudRenderer>()._sourceData;

            threadsize = Mathf.Max(1 + (_sourceData.pointCount / 128), 1);
            kernel = _touchablesShader.FindKernel("Main");

            _touchablesShader.SetBuffer(kernel, "pointsBuffer", _sourceData.computeBuffer);
            _touchablesShader.SetBuffer(kernel, "originalsBuffer" , _sourceData.computeBufferOriginal);
            _touchablesShader.SetBuffer(kernel, "healingProcess", healingProcess);

            _touchablesShader.SetFloat("healParameter", _healParameter);
            _touchablesShader.SetFloat("radiusHandSq", _radiusHandSq);
            
            _touchablesShader.SetVectorArray("radii", boneRadii);

            healingProcessLast = 0;
        }

        /// <summary>
        /// Freeing up working memory, Garbage Collector stuff.
        /// </summary>
        void OnDisable()
        {
            healingProcess.Release();
            healingProcess = null;
        }



        /// <summary>
        /// Scales the hand such that it matches the size of the hand of the user.
        /// </summary>
        /// <param name="scale"> Scale of the hand. We will take it from OVRHand.HandScale </param>
        private void CalibrateHand( float scale )
        {
            for (int i = 0; i < boneRadii.Length; i++) boneRadii[i] *= scale;
        }

        void Update()
        {
            UpdateHandBones();

            if (debugMode || leftHand.IsTracked || rightHand.IsTracked || pointsInMovement)
            {
                if (handNotCalibrated) { 
                    CalibrateHand(rightHand.IsTracked ? rightHand.HandScale : leftHand.HandScale);
                    _touchablesShader.SetVectorArray("radii", boneRadii);
                    handNotCalibrated = false;
                }

                pointsInMovement = false;
                
                if (!touchablePointCloud.activeSelf) return;
                    
                int iFrom = 26; //ifrom and ito define which handbones will be taken into consideration by the GPU (potentially reducing computation significantly).
                int iTo = 26;
                if (debugMode || leftHand.IsTracked)
                    if ((fingertips[0].position - touchablePointCloud.transform.position).magnitude < - touchablePointCloud.GetComponent<PointCloudRenderer>()._cullingRadius)
                    {
                        iFrom = 0;
                    }
                if (debugMode || rightHand.IsTracked)
                    if ((fingertips[5].position - touchablePointCloud.transform.position).magnitude < - touchablePointCloud.GetComponent<PointCloudRenderer>()._cullingRadius)
                    {
                        iTo = 27;
                    }

                if (iTo != iFrom || healingProcessLast != healingProcessCurrent[0])
                {
                    pointsInMovement |= healingProcessLast != healingProcessCurrent[0];
                    healingProcessLast = healingProcessCurrent[0];
                    _touchablesShader.SetMatrix("_Transform", touchablePointCloud.transform.localToWorldMatrix);
                    _touchablesShader.SetMatrix("_TransformInv", touchablePointCloud.transform.worldToLocalMatrix);
                    _touchablesShader.SetInt("iFrom", iFrom);
                    _touchablesShader.SetInt("iTo", iTo);
                    _touchablesShader.SetVectorArray("HandBones", handBones);
                    _touchablesShader.Dispatch(kernel, threadsize, 1, 1);
                    healingProcess.GetData(healingProcessCurrent);
                }
            }
        }
        #endregion
    }
}