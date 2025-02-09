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
using UdonSharp.Internal;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    [CustomEditor(typeof(LightSync), true), CanEditMultipleObjects]

    public class LightSyncEditor : Editor
    {
        public static bool foldoutOpen = false;

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(targets))
            {
                return;
            }
            if (!Application.isPlaying)
            {
                int syncCount = 0;
                int pickupSetupCount = 0;
                int rigidSetupCount = 0;
                int respawnYSetupCount = 0;
                int stateSetupCount = 0;
                foreach (var t in targets)
                {
                    var sync = (LightSync)t;
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
                            if (sync.enterFirstCustomStateOnStart && stateComponents.Length > 0 && stateComponents[0].stateID != 0)
                            {
                                errorFound = true;
                            }
                            else
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
                        }
                        if (!errorFound)
                        {
                            errorFound = sync.enterFirstCustomStateOnStart && sync.state < 0;
                        }
                        if (!errorFound)
                        {
                            errorFound = !sync.enterFirstCustomStateOnStart && sync.state >= 0;
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
                        SetupLightSyncs(targets);
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
                        MatchRespawnHeights(targets);
                    }
                }
            }

            EditorGUILayout.Space();
            base.OnInspectorGUI();
            EditorGUILayout.Space();

            foldoutOpen = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutOpen, "Advanced Settings");
            if (foldoutOpen)
            {
                if (GUILayout.Button(new GUIContent("Force Setup")))
                {
                    ForceSetup(targets);
                }
                ShowAdvancedOptions();
                serializedObject.ApplyModifiedProperties();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (foldoutOpen)
            {
                EditorGUILayout.Space();
                ShowInternalObjects();
            }
        }

        readonly string[] serializedPropertyNames = {
            "debugLogs",
            "showInternalObjects",
            "enterFirstCustomStateOnStart",
            "separateHelperObjects",
            "kinematicWhileAttachedToPlayer",
            "controlPickupableState",
            "useWorldSpaceTransforms",
            "useWorldSpaceTransformsWhenHeldOrAttachedToPlayer",
            "syncCollisions",
            "syncParticleCollisions",
            "allowOutOfOrderData",
            "takeOwnershipOfOtherObjectsOnCollision",
            "allowOthersToTakeOwnershipOnCollision",
            "positionDesyncThreshold",
            "rotationDesyncThreshold",
            "minimumSleepFrames",
        };

        readonly string[] serializedInternalObjectNames = {
            "singleton",
            "data",
            "looper",
            "fixedLooper",
            "lateLooper",
            "rigid",
            "pickup",
            "customStates",
            "_behaviourEventListeners",
            "_classEventListeners",
        };

        IEnumerable<SerializedProperty> serializedProperties;
        IEnumerable<SerializedProperty> serializedInternalObjects;
        public void OnEnable()
        {
            serializedProperties = serializedPropertyNames.Select(propName => serializedObject.FindProperty(propName));
            serializedInternalObjects = serializedInternalObjectNames.Select(propName => serializedObject.FindProperty(propName));
        }
        void ShowAdvancedOptions()
        {
            EditorGUI.indentLevel++;
            GUI.enabled = false;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_state"));
            GUI.enabled = true;
            foreach (var property in serializedProperties)
            {
                if (property != null)
                {
                    EditorGUILayout.PropertyField(property);
                }
            }
            EditorGUI.indentLevel--;
        }

        void ShowInternalObjects()
        {
            EditorGUILayout.LabelField("Internal Objects", EditorStyles.boldLabel);
            GUI.enabled = false;
            EditorGUI.indentLevel++;
            foreach (var property in serializedInternalObjects)
            {
                if (property != null)
                {
                    EditorGUILayout.PropertyField(property);
                }
            }
            EditorGUI.indentLevel--;
            GUI.enabled = true;
        }

        public static void SetupLightSyncs(Object[] objects)
        {
            bool syncFound = false;
            foreach (var obj in objects)
            {
                var sync = (LightSync)obj;
                if (Utilities.IsValid(sync))
                {
                    syncFound = true;
                    sync.AutoSetup();
                }
            }

            if (!syncFound)
            {
                Debug.LogWarningFormat("[LightSync] Auto Setup failed: No LightSync selected");
            }
        }
        public static void ForceSetup(Object[] objects)
        {
            foreach (var obj in objects)
            {
                var sync = (LightSync)obj;
                if (Utilities.IsValid(sync))
                {
                    sync.ForceSetup();
                }
            }
        }

        public static void MatchRespawnHeights(Object[] objects)
        {
            bool syncFound = false;
            foreach (var obj in objects)
            {
                var sync = (LightSync)obj;
                if (Utilities.IsValid(sync))
                {
                    syncFound = true;
                    sync.respawnHeight = VRC_SceneDescriptor.Instance.RespawnHeightY;
                }
            }

            if (!syncFound)
            {
                Debug.LogWarningFormat("[LightSync] Auto Setup failed: No LightSync selected");
            }
        }

        public void OnDestroy()
        {
            if (target == null && Application.isPlaying == false)
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
                    data.DestroyAsync();
                }
            }
            foreach (var looper in FindObjectsOfType<LightSyncLooper>())
            {
                if (looper.sync == null || looper.sync.looper != looper)
                {
                    looper.DestroyAsync();
                }
            }
            foreach (var stateData in FindObjectsOfType<LightSyncStateData>())
            {
                if (stateData.state == null || stateData.state.data != stateData)
                {
                    stateData.DestroyAsync();
                }
            }
            foreach (var enhancementData in FindObjectsOfType<LightSyncEnhancementData>())
            {
                if (enhancementData.enhancement == null || enhancementData.enhancement.enhancementData != enhancementData)
                {
                    enhancementData.DestroyAsync();
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
            if (change != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }
            if (!EditorPrefs.GetBool(autoSetupKey, true))
            {
                return;
            }
            AutoSetup();
        }

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            if (!EditorPrefs.GetBool(autoSetupKey, true))
            {
                return true;
            }
            return AutoSetup();
        }

        public static void SetupLightSyncListener(LightSyncListener listener)
        {
            if (!Utilities.IsValid(listener))
            {
                return;
            }
            if (!IsEditable(listener))
            {
                Debug.LogErrorFormat(listener, "<color=red>[LightSync AutoSetup]: ERROR</color> {0}", "LightSyncListener is not editable");
            }
            else
            {
                listener.AutoSetup();
                new SerializedObject(listener).Update();
                PrefabUtility.RecordPrefabInstancePropertyModifications(listener);
            }
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
        private const string MenuItemPath = "MMMaellon/LightSync/Automatically run setup";
        private const string autoSetupKey = "MyToggleFeatureEnabled";
        [MenuItem(MenuItemPath)]
        private static void ToggleAutoSetup()
        {
            var autoSetupOn = EditorPrefs.GetBool(autoSetupKey, true);
            autoSetupOn = !autoSetupOn;
            Menu.SetChecked(MenuItemPath, autoSetupOn);
            EditorPrefs.SetBool(autoSetupKey, autoSetupOn);
        }

        [MenuItem(MenuItemPath, true)]
        private static bool ValidateToggleAutoSetup()
        {
            var autoSetupOn = EditorPrefs.GetBool(autoSetupKey, true);
            Menu.SetChecked(MenuItemPath, autoSetupOn);
            return true;
        }

        [MenuItem("MMMaellon/LightSync/Run setup")]
        public static bool ForceAutoSetup()
        {
            return AutoSetup(false);
        }
        public static bool AutoSetup(bool skipAlreadySetup = true)
        {
            Debug.Log("Running LightSync AutoSetup");
            ClearOrphanedObjects();
            foreach (LightSyncListener listener in GameObject.FindObjectsOfType<LightSyncListener>(true))
            {
                SetupLightSyncListener(listener);
            }
            var allLightSyncs = GameObject.FindObjectsByType<LightSync>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            if (allLightSyncs.Length > 0)
            {
                var allSingletons = GameObject.FindObjectsOfType<Singleton>(true);
                Singleton singleton;
                if (allSingletons.Length >= 1)
                {
                    singleton = allSingletons[0];
                    for (int i = 1; i < allSingletons.Length; i++)
                    {
                        GameObject.DestroyImmediate(allSingletons[i].gameObject);
                    }
                }
                else
                {
                    GameObject singletonObject = new("LightSync Singleton");
                    singleton = UdonSharpComponentExtensions.AddUdonSharpComponent<Singleton>(singletonObject);
                }
                singleton.lightSyncs = allLightSyncs;
                // singleton.gameObject.hideFlags |= HideFlags.HideInHierarchy;
                singleton.AutoSetup(skipAlreadySetup);
            }
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            return true;
        }

        [MenuItem("MMMaellon/LightSync/Clear Orphaned Internal Objects")]
        public static void ClearOrphanedObjects()
        {
            foreach (var obj in GameObject.FindObjectsOfType<GameObject>(true))
            {
                var data = obj.GetComponents<LightSyncData>();
                var stateData = obj.GetComponents<LightSyncStateData>();
                var enhancementData = obj.GetComponents<LightSyncEnhancementData>();
                var looper = obj.GetComponents<LightSyncLooper>();
                var singleton = obj.GetComponents<Singleton>();
                foreach (var d in data)
                {
                    d.RefreshHideFlags();
                    if (d.sync == null)
                    {
                        d.DestroyAsync();
                    }
                }
                foreach (var d in stateData)
                {
                    d.RefreshHideFlags();
                    if (d.state == null)
                    {
                        d.DestroyAsync();
                    }
                }
                foreach (var d in enhancementData)
                {
                    d.RefreshHideFlags();
                    if (d.enhancement == null)
                    {
                        d.DestroyAsync();
                    }
                }
                foreach (var l in looper)
                {
                    l.RefreshHideFlags();
                    if (l.sync == null)
                    {
                        l.DestroyAsync();
                    }
                }
            }
        }

        [MenuItem("MMMaellon/LightSync/Show all helper objects")]
        public static bool ShowAllHiddenGameObjects()
        {
            foreach (var obj in GameObject.FindObjectsOfType<GameObject>(true))
            {
                var data = obj.GetComponents<LightSyncData>();
                var stateData = obj.GetComponents<LightSyncStateData>();
                var EnhancementData = obj.GetComponents<LightSyncEnhancementData>();
                var looper = obj.GetComponents<LightSyncLooper>();
                var singleton = obj.GetComponents<Singleton>();
                foreach (var d in data)
                {
                    d.gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
                    d.hideFlags &= ~HideFlags.HideInInspector;
                }
                foreach (var d in stateData)
                {
                    d.gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
                    d.hideFlags &= ~HideFlags.HideInInspector;
                }
                foreach (var d in EnhancementData)
                {
                    d.gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
                    d.hideFlags &= ~HideFlags.HideInInspector;
                }
                foreach (var l in looper)
                {
                    l.gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
                    l.hideFlags &= ~HideFlags.HideInInspector;
                }
                foreach (var s in singleton)
                {
                    s.gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
                    s.hideFlags &= ~HideFlags.HideInInspector;
                }
                //uncomment to just show everything
                // obj.hideFlags &= ~HideFlags.HideInHierarchy;
            }
            return true;
        }
    }
}
#endif
