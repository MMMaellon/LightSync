using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Components;


#if !COMPILER_UDONSHARP && UNITY_EDITOR
using VRC.SDKBase.Editor.BuildPipeline;
using UnityEditor;
using UdonSharpEditor;
using System.Collections.Generic;
using System.Linq;
#endif

namespace MMMaellon.LightSync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None), RequireComponent(typeof(Rigidbody))]
    public class LightSync : UdonSharpBehaviour
    {
        //Gets created in the editor, as an invisible child of this object. Because it's a separate object we can sync it's data separately from the others on this object
        [HideInInspector]
        public LightSyncData data;
        [HideInInspector]
        public LightSyncLooper looper;
        [HideInInspector]
        public LightSyncPhysicsDispatcher dispatcher;
        [HideInInspector]
        public Rigidbody rigid;
        [HideInInspector]
        public VRC_Pickup pickup;

        //Settings
        public float respawnHeight = -1001f;
        [Tooltip("Controls how long it takes for the object to smoothly move into the synced position. Set to negative or 0 for auto.")]
        public float smoothing = 0f;
        public bool allowTheftFromSelf = true;
        public bool allowTheftWhenAttachedToPlayer = true;
        public bool kinematicWhileHeld = true;
        public bool forceNonkinematicPhysics = true;
        [Tooltip("Costs performance, but is required if a custom script changes the transform of this object")]
        public bool forceRunEveryFrame = false;

        //Extensions
        [HideInInspector]
        public LightSyncListener[] eventListeners = new LightSyncListener[0];
        [HideInInspector]
        public LightSyncState[] customStates = new LightSyncState[0];
        [HideInInspector]
        public bool enterFirstCustomStateOnStart = false;

        //advanced settings
        [HideInInspector]
        public bool debugLogs = false;
        [HideInInspector]
        public bool kinematicWhileAttachedToPlayer = true;
        [HideInInspector]
        public bool attachToAvatarBonesWhenPickedUp = true;
        [HideInInspector]
        public bool syncParticleCollisions = true;
        [HideInInspector]
        public bool takeOwnershipOnPickup = true;
        [HideInInspector]
        public bool takeOwnershipOfOtherObjectsOnCollision = true;
        [HideInInspector]
        public bool allowOthersToTakeOwnershipOnCollision = true;


        public void _print(string message)
        {
            Debug.LogFormat(this, "[LightSync] {0}: {1}", name, message);
        }

        public bool IsOwner()
        {
            return data.IsOwner();
        }

        public void Sync()
        {
            if (!IsOwner())
            {
                Networking.SetOwner(Networking.LocalPlayer, data.gameObject);
            }
            data.RequestSerialization();
        }

        int lastCollisionEnter = -1001;
        public void OnCollisionEnter(Collision other)
        {
            if (lastCollisionEnter == Time.frameCount)
            {
                return;
            }
            lastCollisionEnter = Time.frameCount;
            OnCollision();
        }

        int lastCollisionExit = -1001;
        public void OnCollisionExit(Collision other)
        {
            if (lastCollisionExit == Time.frameCount)
            {
                return;
            }
            lastCollisionExit = Time.frameCount;
            OnCollision();
        }

        public void OnParticleCollision(GameObject other)
        {
            if (!syncParticleCollisions || other == gameObject || lastCollisionEnter == Time.frameCount)
            {
                return;
            }
            lastCollisionEnter = Time.frameCount;
            OnCollision();
        }

        public void OnCollision()
        {
            if (data.IsOwner())
            {
                if (data.state == LightSyncData.STATE_PHYSICS || data.state == LightSyncData.STATE_SLEEP)
                {
                    data.state = LightSyncData.STATE_PHYSICS;
                    Sync();
                }
            }
        }

        public override void OnPickup()
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }

        public override void OnDrop()
        {

        }
        public bool IsHeld
        {
            get => data.state <= LightSyncData.STATE_LEFT_HAND && data.state >= LightSyncData.STATE_NO_HAND;
        }
        public bool IsAttachedToPlayer
        {
            get => data.state <= LightSyncData.STATE_LOCAL_TO_OWNER;
        }

        public void OnEnable()
        {
            if (data)
            {
                Networking.SetOwner(Networking.GetOwner(gameObject), data.gameObject);
            }
        }
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && player.isLocal)
            {
                Networking.SetOwner(player, data.gameObject);
            }
        }

        public void ChangeState(sbyte newStateID)
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            data.state = newStateID;
            Sync();
        }

#if !UNITY_EDITOR
        public float lagTime
        {
            get => Time.realtimeSinceStartup - Networking.SimulationTime(gameObject);
        }
#else
        public float lagTime
        {
            get => 0.25f;
        }
#endif
        float lerpStartTime;
        bool lastKinematic;
        bool changedKinematic;
        public void OnEnterState()
        {
            if (data.state >= 0 && data.state < customStates.Length)
            {
                customStates[data.state].OnEnterState();
                return;
            }
            lastKinematic = rigid.isKinematic;
            if (kinematicWhileHeld && IsHeld)
            {
                rigid.isKinematic = true;
                changedKinematic = true;
            }
            else if (kinematicWhileAttachedToPlayer && data.state < LightSyncData.STATE_NO_HAND)
            {
                rigid.isKinematic = true;
                changedKinematic = true;
            }
            else if (forceNonkinematicPhysics && data.state == LightSyncData.STATE_PHYSICS)
            {
                rigid.isKinematic = false;
                changedKinematic = true;
            }
            else if (data.state == LightSyncData.STATE_SPAWN)
            {
                rigid.isKinematic = true;
                changedKinematic = true;
            }
            else
            {
                changedKinematic = false;
            }
        }

        public void OnExitState()
        {
            if (data.state >= 0 && data.state < customStates.Length)
            {
                customStates[data.state].OnExitState();
                return;
            }
            if (changedKinematic)
            {
                rigid.isKinematic = lastKinematic;
            }
        }

        public void OnSendingData()
        {
            if (data.state >= 0 && data.state < customStates.Length)
            {
                customStates[data.state].OnSendingData();
                return;
            }
        }

        public float GetInterpolation()
        {
            if (IsOwner())
            {
                return 1.0f;
            }
            else if (smoothing > 0)
            {
                return smoothing;
            }
            return lagTime <= 0 ? 1 : Mathf.Lerp(0, 1, (Time.timeSinceLevelLoad - lerpStartTime) / lagTime);
        }

        public void OnLerpStart()
        {
            lerpStartTime = Time.timeSinceLevelLoad;
            looper.enabled = true;
            if (data.state >= 0 && data.state < customStates.Length)
            {
                customStates[data.state].OnLerpStart();
                return;
            }
        }

        bool continueLerp;
        public void OnLerp()
        {
            if (data.state >= 0 && data.state < customStates.Length)
            {
                customStates[data.state].OnLerp();
                if (customStates[data.state].GetInterpolation() >= 1)
                {
                    OnLerpEnd();
                }
                return;
            }

            if (GetInterpolation() >= 1)
            {
                OnLerpEnd();
            }
        }

        public void OnLerpEnd()
        {
            if (data.state >= 0 && data.state < customStates.Length)
            {
                continueLerp = customStates[data.state].OnLerpEnd();
                return;
            }
            if (!continueLerp)
            {
                looper.enabled = false;
                dispatcher.Dispatch();
            }
        }

        public void OnPhysicsDispatch()
        {
            if (data.state >= 0 && data.state < customStates.Length)
            {
                customStates[data.state].OnPhysicsDispatch();
                return;
            }
        }


#if UNITY_EDITOR && !COMPILER_UDONSHARP
        bool _showInternalObjects = false;
        [HideInInspector]
        public bool showInternalObjects = false;

        public void Reset()
        {
            AutoSetup();

            respawnHeight = VRC_SceneDescriptor.Instance.RespawnHeightY;

            rigid.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rigid.interpolation = RigidbodyInterpolation.Interpolate;

        }

        public void OnValidate()
        {
            AutoSetup();
        }

        public void RefreshHideFlags()
        {
            if (_showInternalObjects != showInternalObjects)
            {
                _showInternalObjects = showInternalObjects;
                data.RefreshHideFlags();
                looper.RefreshHideFlags();
                dispatcher.RefreshHideFlags();
            }
        }

        public void AutoSetup()
        {
            _print("Auto setup");

            rigid = GetComponent<Rigidbody>();
            pickup = GetComponent<VRC_Pickup>();
            CreateDataObject();
            CreateLooperObject();
            RefreshHideFlags();
            SetupStates();
            SetupListeners();
        }

        public void SetupStates()
        {
            customStates = GetComponents<LightSyncState>();
            _print("Found " + customStates.Length + " custom states");
            for (int i = 0; i < customStates.Length; i++)
            {
                customStates[i].stateID = i;
                customStates[i].sync = this;
                customStates[i].data = data;
            }
        }

        public void SetupListeners()
        {
            eventListeners = eventListeners.Where(obj => Utilities.IsValid(obj)).ToArray();
        }

        public void CreateDataObject()
        {
            if (data != null)
            {
                if (data.transform.parent != transform)
                {
                    data.transform.SetParent(transform, false);
                }

                if (data.sync != this)
                {
                    data.sync = this;
                }

                return;
            }
            GameObject dataObject = new(name + "_data");
            dataObject.transform.SetParent(transform, false);
            data = dataObject.AddComponent<LightSyncData>();
            data.sync = this;
            data.RefreshHideFlags();
        }

        public void CreateLooperObject()
        {
            if (looper != null)
            {
                if (looper.transform.parent != transform)
                {
                    looper.transform.SetParent(transform, false);
                }

                if (looper.sync != this)
                {
                    looper.sync = this;
                }

                if (looper.data != data)
                {
                    looper.data = data;
                }

                return;
            }
            GameObject looperObject = new(name + "_looper");
            looperObject.transform.SetParent(transform, false);
            looper = looperObject.AddComponent<LightSyncLooper>();
            looper.sync = this;
            looper.data = data;
            looper.RefreshHideFlags();
        }

        public void CreateDispatcherObject()
        {
            if (dispatcher != null)
            {
                if (dispatcher.transform.parent != transform)
                {
                    dispatcher.transform.SetParent(transform, false);
                }

                if (dispatcher.sync != this)
                {
                    dispatcher.sync = this;
                }

                return;
            }
            GameObject dispatcherObject = new(name + "_dispatcher");
            dispatcherObject.transform.SetParent(transform, false);
            dispatcher = dispatcherObject.AddComponent<LightSyncPhysicsDispatcher>();
            dispatcher.sync = this;
            dispatcher.RefreshHideFlags();
        }

        public void OnDestroy()
        {
            if (data)
            {
                Destroy(data.gameObject);
            }
            if (looper)
            {
                Destroy(looper.gameObject);
            }
        }

#endif
    }
}

#if !COMPILER_UDONSHARP && UNITY_EDITOR
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
                    EditorGUILayout.HelpBox(stateSetupCount.ToString() + @" SmartObjectSyncs with misconfigured States", MessageType.Warning);
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
                if (GUILayout.Button(new GUIContent("Debug")))
                {
                    ((LightSync)target).DebugThing();
                }
                ShowAdvancedOptions();
                serializedObject.ApplyModifiedProperties();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }



        SerializedProperty m_debugLogs;
        SerializedProperty m_showInternalObjects;
        SerializedProperty m_kinematicWhileAttachedToPlayer;
        SerializedProperty m_attachToAvatarBonesWhenPickedUp;
        SerializedProperty m_syncParticleCollisions;
        SerializedProperty m_takeOwnershipOnPickup;
        SerializedProperty m_takeOwnershipOfOtherObjectsOnCollision;
        SerializedProperty m_allowOthersToTakeOwnershipOnCollision;
        string[] serializedPropertyNames = {

        };
        SerializedProperty[] serializedProperties;
        void OnEnable()
        {
            serializedProperties = serializedPropertyNames.Where(propName => serializedObject.FindProperty(propName));
            m_debugLogs = serializedObject.FindProperty("debugLogs");
            m_showInternalObjects = serializedObject.FindProperty("showInternalObjects");
            m_kinematicWhileAttachedToPlayer = serializedObject.FindProperty("kinematicWhileAttachedToPlayer");
            m_attachToAvatarBonesWhenPickedUp = serializedObject.FindProperty("attachToAvatarBonesWhenPickedUp");
            m_syncParticleCollisions = serializedObject.FindProperty("syncParticleCollisions");
            m_takeOwnershipOnPickup = serializedObject.FindProperty("takeOwnershipOnPickup");
            m_takeOwnershipOfOtherObjectsOnCollision = serializedObject.FindProperty("takeOwnershipOfOtherObjectsOnCollision");
            m_allowOthersToTakeOwnershipOnCollision = serializedObject.FindProperty("allowOthersToTakeOwnershipOnCollision");
        }
        void ShowAdvancedOptions()
        {
            EditorGUILayout.PropertyField(m_debugLogs);
            EditorGUILayout.PropertyField(m_showInternalObjects);
            EditorGUILayout.PropertyField(m_attachToAvatarBonesWhenPickedUp);
            EditorGUILayout.PropertyField(m_syncParticleCollisions);
            EditorGUILayout.PropertyField(m_takeOwnershipOnPickup);
            EditorGUILayout.PropertyField(m_takeOwnershipOfOtherObjectsOnCollision);
            EditorGUILayout.PropertyField(m_allowOthersToTakeOwnershipOnCollision);
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
    }
}
#endif
