#if !COMPILER_UDONSHARP && UNITY_EDITOR
using VRC.SDKBase.Editor.BuildPipeline;
using UnityEditor;
using UdonSharpEditor;
using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace MMMaellon.LightSync
{
    [CustomEditor(typeof(LightSync), true), CanEditMultipleObjects]

    public class LightSyncEditor : Editor
    {
        public static bool foldoutOpen = false;

        public override void OnInspectorGUI()
        {
            int syncCount = 0;
            int pickupSetupCount = 0;
            int rigidSetupCount = 0;
            int respawnYSetupCount = 0;
            int stateSetupCount = 0;
            foreach (LightSync sync in Selection.GetFiltered<LightSync>(SelectionMode.Editable))
            {
                if (!Utilities.IsValid(sync))
                {
                    continue;
                }
                syncCount++;
                if (sync.pickup != sync.GetComponent<VRC_Pickup>())
                {
                    pickupSetupCount++;
                }
                if (sync.rigid != sync.GetComponent<Rigidbody>())
                {
                    rigidSetupCount++;
                }
                if (Utilities.IsValid(VRC_SceneDescriptor.Instance) && !Mathf.Approximately(VRC_SceneDescriptor.Instance.RespawnHeightY, sync.respawnHeight))
                {
                    respawnYSetupCount++;
                }
                LightSyncState[] stateComponents = sync.GetComponents<LightSyncState>();
                if (sync.customStates.Length != stateComponents.Length)
                {
                    stateSetupCount++;
                }
                else
                {
                    bool errorFound = false;
                    foreach (LightSyncState state in sync.customStates)
                    {
                        if (state == null || state.sync != sync || state.stateID < 0 || state.stateID >= sync.customStates.Length || sync.customStates[state.stateID] != state)
                        {
                            errorFound = true;
                            break;
                        }
                    }
                    if (!errorFound)
                    {
                        foreach (LightSyncState state in stateComponents)
                        {
                            if (state != null && (state.sync != sync || state.stateID < 0 || state.stateID >= sync.customStates.Length || sync.customStates[state.stateID] != state))
                            {
                                errorFound = true;
                                break;
                            }
                        }
                    }
                    if (errorFound)
                    {
                        stateSetupCount++;
                    }
                }
            }
            if (pickupSetupCount > 0 || rigidSetupCount > 0 || stateSetupCount > 0)
            {
                if (pickupSetupCount == 1)
                {
                    EditorGUILayout.HelpBox(@"Object not set up for VRC_Pickup", MessageType.Warning);
                }
                else if (pickupSetupCount > 1)
                {
                    EditorGUILayout.HelpBox(pickupSetupCount.ToString() + @" Objects not set up for VRC_Pickup", MessageType.Warning);
                }
                if (rigidSetupCount == 1)
                {
                    EditorGUILayout.HelpBox(@"Object not set up for Rigidbody", MessageType.Warning);
                }
                else if (rigidSetupCount > 1)
                {
                    EditorGUILayout.HelpBox(rigidSetupCount.ToString() + @" Objects not set up for Rigidbody", MessageType.Warning);
                }
                if (stateSetupCount == 1)
                {
                    EditorGUILayout.HelpBox(@"States misconfigured", MessageType.Warning);
                }
                else if (stateSetupCount > 1)
                {
                    EditorGUILayout.HelpBox(stateSetupCount.ToString() + @" LightSyncs with misconfigured States", MessageType.Warning);
                }
                if (GUILayout.Button(new GUIContent("Auto Setup")))
                {
                    SetupSelectedLightSyncs();
                }
            }
            if (respawnYSetupCount > 0)
            {
                if (respawnYSetupCount == 1)
                {
                    EditorGUILayout.HelpBox(@"Respawn Height is different from the scene descriptor's: " + VRC_SceneDescriptor.Instance.RespawnHeightY, MessageType.Info);
                }
                else if (respawnYSetupCount > 1)
                {
                    EditorGUILayout.HelpBox(respawnYSetupCount.ToString() + @" Objects have a Respawn Height that is different from the scene descriptor's: " + VRC_SceneDescriptor.Instance.RespawnHeightY, MessageType.Info);
                }
                if (GUILayout.Button(new GUIContent("Match Scene Respawn Height")))
                {
                    MatchRespawnHeights();
                }
            }
            if (target && UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
            {
                return;
            }

            EditorGUILayout.Space();
            base.OnInspectorGUI();
            EditorGUILayout.Space();

            foldoutOpen = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutOpen, "Advanced Settings");
            if (foldoutOpen)
            {
                if (GUILayout.Button(new GUIContent("Force Setup")))
                {
                    SetupSelectedLightSyncs();
                }
                ShowAdvancedOptions();
                serializedObject.ApplyModifiedProperties();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        readonly string[] serializedPropertyNames = {
        "debugLogs",
        "showInternalObjects",
        "kinematicWhileAttachedToPlayer",
        "useWorldSpaceTransforms",
        "useWorldSpaceTransformsWhenHeldOrAttachedToPlayer",
        "syncParticleCollisions",
        "takeOwnershipOfOtherObjectsOnCollision",
        "allowOthersToTakeOwnershipOnCollision",
        "positionDesyncThreshold",
        "rotationDesyncThreshold",
        "minimumSleepFrames",
        };

        IEnumerable<SerializedProperty> serializedProperties;
        public void OnEnable()
        {
            serializedProperties = serializedPropertyNames.Select(propName => serializedObject.FindProperty(propName));
        }
        void ShowAdvancedOptions()
        {
            foreach (var property in serializedProperties)
            {
                EditorGUILayout.PropertyField(property);
            }
        }

        public static void SetupSelectedLightSyncs()
        {
            bool syncFound = false;
            foreach (LightSync sync in Selection.GetFiltered<LightSync>(SelectionMode.Editable))
            {
                syncFound = true;
                sync.AutoSetup();
            }

            if (!syncFound)
            {
                Debug.LogWarningFormat("[LightSync] Auto Setup failed: No LightSync selected");
            }
        }
        public static void MatchRespawnHeights()
        {
            bool syncFound = false;
            foreach (LightSync sync in Selection.GetFiltered<LightSync>(SelectionMode.Editable))
            {
                syncFound = true;
                sync.respawnHeight = VRC_SceneDescriptor.Instance.RespawnHeightY;
            }

            if (!syncFound)
            {
                Debug.LogWarningFormat("[LightSync] Auto Setup failed: No LightSync selected");
            }
        }
        public void OnDestroy()
        {
            if (target == null)
            {
                CleanHelperObjects();
            }
        }

        public void CleanHelperObjects()
        {
            foreach (var data in FindObjectsOfType<LightSyncData>())
            {
                if (data.sync == null || data.sync.data != data)
                {
                    data.StartCoroutine(data.Destroy());
                }
            }
            foreach (var looper in FindObjectsOfType<LightSyncLooperUpdate>())
            {
                if (looper.sync == null || looper.sync.looper != looper)
                {
                    looper.StartCoroutine(looper.Destroy());
                }
            }
        }
    }

    [InitializeOnLoad]
    public class LightSyncBuildHandler : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => 0;
        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;
            Setup();
        }
        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            return Setup();
        }
        public static void SetupLightSync(LightSync sync)
        {
            if (!Utilities.IsValid(sync))
            {
                return;
            }
            if (!IsEditable(sync))
            {
                Debug.LogErrorFormat(sync, "<color=red>[LightSync AutoSetup]: ERROR</color> {0}", "LightSync is not editable");
            }
            else
            {
                sync.AutoSetup();
            }
        }
        public static bool IsEditable(Component component)
        {
            return !EditorUtility.IsPersistent(component.transform.root.gameObject) && !(component.gameObject.hideFlags == HideFlags.NotEditable || component.gameObject.hideFlags == HideFlags.HideAndDontSave);
        }


        public static bool Setup()
        {
            foreach (LightSync sync in GameObject.FindObjectsOfType<LightSync>())
            {
                SetupLightSync(sync);
            }
            return true;
        }
    }
}
#endif
