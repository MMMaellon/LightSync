#if !COMPILER_UDONSHARP && UNITY_EDITOR
using VRC.SDKBase.Editor.BuildPipeline;
using UnityEditor;
using UdonSharpEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDKBase;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MMMaellon.LightSync
{
    [CustomEditor(typeof(LightSyncState), true)]
    [CanEditMultipleObjects]
    public class LightSyncStateEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var state = (LightSyncState)target;
            if (state.sync)
            {
                var originalColor = GUI.backgroundColor;
                if (state.sync.state == state.stateID)
                {
                    GUI.backgroundColor = Color.yellow;
                }
                if (state.sync.state == state.stateID)
                {
                    if (Application.isPlaying)
                    {

                        EditorGUILayout.HelpBox("Active State", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Starting State", MessageType.Info);
                    }
                }
                GUI.backgroundColor = originalColor;
            }
            EditorGUILayout.Space();
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(targets))
            {
                return;
            }
            EditorGUILayout.Space();
            base.OnInspectorGUI();
        }
    }
}
#endif
