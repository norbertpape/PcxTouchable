// Touchable point clouds
// by Norbert Pape and Simon Speiser
// using point clouds imported with Keijiro's
// norbertpape111@gmail.com
// Extension of Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx

using UnityEngine;
using UnityEditor;
#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

namespace PcxTouchable
{
    [CustomEditor(typeof(PlyImporter))]
    class PlyImporterInspector : ScriptedImporterEditor
    {
        SerializedProperty _healingStyle;
        SerializedProperty _pointCountCap;

        protected override bool useAssetDrawPreview { get { return false; } }

        public override void OnEnable()
        {
            base.OnEnable();

            _healingStyle = serializedObject.FindProperty("_healingStyle");
            _pointCountCap = serializedObject.FindProperty("_pointCountCap");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.PropertyField(_healingStyle);
            EditorGUILayout.PropertyField(_pointCountCap);
            base.ApplyRevertGUI();
        }
    }
}
