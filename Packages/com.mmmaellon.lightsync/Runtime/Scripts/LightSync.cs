using System.Collections;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
#endif

namespace MMMaellon.LightSync
{
    [AddComponentMenu("LightSync/LightSync")]
    [RequireComponent(typeof(Rigidbody))]
    public class LightSync : UdonSharpBehaviour
    {

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        public enum NetworkDataOptimization
        {
            Ultra,
            High,
            Low,
            Unoptimized,
            DisableNetworking,
        }

        public NetworkDataOptimization networkDataOptimization = NetworkDataOptimization.Ultra;
#else
        //to get rid of some errors
        [System.NonSerialized]
        int networkDataOptimization;
#endif
        [HideInInspector]
        public Singleton singleton;

        //Gets created in the editor, as an invisible child of this object. Because it's a separate object we can sync it's data separately from the others on this object
        [HideInInspector]
        public LightSyncData data;
        [HideInInspector]
        public LightSyncLooperUpdate looper;
        [HideInInspector]
        public LightSyncLooperFixedUpdate fixedLooper;
        [HideInInspector]
        public LightSyncLooperPostLateUpdate lateLooper;
        [HideInInspector]
        public Rigidbody rigid;
        [HideInInspector]
        public VRC_Pickup pickup;
        [HideInInspector]
        public uint id = 1001;

        //Settings
        [Space]
        public float respawnHeight = -1001f;
        [Tooltip("Controls how long it takes for the object to smoothly move into the synced position. Set to 0 for VRChat's algorithm. Set negative for my autosmoothing algorithm. The more negative the smoother. Set to positive for a fixed smoothing time.")]
        public float smoothingTime = -0.25f;
        [Space]
        public bool allowTheftFromSelf = true;
        public bool allowTheftWhenAttachedToPlayer = true;
        public bool kinematicWhileHeld = true;
        [Space]
        public bool syncIsKinematic = true;
        public bool syncPickupable = false;
        [Space]
        public bool sleepOnSpawn = true;
        [Tooltip("Costs performance, but is required if a custom script changes the transform of this object")]
        public bool runEveryFrameOnOwner = false;

        //Extensions
        public Component[] eventListeners = new Component[0];
        [SerializeField, HideInInspector]
        public UdonBehaviour[] _behaviourEventListeners = new UdonBehaviour[0];
        [SerializeField, HideInInspector]
        public LightSyncListener[] _classEventListeners = new LightSyncListener[0];

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
        public bool controlPickupableState = true;
        [HideInInspector]
        public bool useWorldSpaceTransforms = false;
        [HideInInspector]
        public bool useWorldSpaceTransformsWhenHeldOrAttachedToPlayer = false;
        [HideInInspector]
        public bool syncCollisions = true;
        [HideInInspector]
        public bool syncParticleCollisions = true;
        [HideInInspector]
        public bool allowOutOfOrderData = false;
        [HideInInspector]
        public bool takeOwnershipOfOtherObjectsOnCollision = true;
        [HideInInspector]
        public bool allowOthersToTakeOwnershipOnCollision = true;
        [HideInInspector]
        public float positionDesyncThreshold = 0.015f;
        [HideInInspector]
        public float rotationDesyncThreshold = 0.995f;
        [HideInInspector]
        public int minimumSleepFrames = 4;

        [HideInInspector]
        public Vector3 spawnPos;
        [HideInInspector]
        public Quaternion spawnRot;

        //Set from the data object

        [System.NonSerialized]
        public sbyte prevState;
        [SerializeField, HideInInspector]
        sbyte _state = STATE_PHYSICS;
        public sbyte state
        {
            get => _state;
            set
            {
                prevState = _state;
                _state = value;
                OnExitState();
                OnEnterState();
            }
        }

        [System.NonSerialized]
        public Vector3 pos;
        [System.NonSerialized]
        public Quaternion rot;
        [System.NonSerialized]
        public Vector3 vel;
        [System.NonSerialized]
        public Vector3 spin;

        [System.NonSerialized]
        public byte syncCount;
        [System.NonSerialized]
        public byte localSyncCount; //counts up every time we receive new data
        [System.NonSerialized]
        public byte teleportCount = 1;
        [System.NonSerialized]
        public byte localTeleportCount; //counts up every time we teleport
        [System.NonSerialized]
        public int loopTimingFlag = LOOP_POSTLATEUPDATE;

        public const int LOOP_UPDATE = 0;
        public const int LOOP_FIXEDUPDATE = 1;
        public const int LOOP_POSTLATEUPDATE = 2;

        public virtual bool teleportFlag
        {
            get => localTeleportCount != teleportCount;
            set
            {
                if (value)
                {
                    lastTeleport = Time.frameCount;
                    if (IsOwner())
                    {
                        if (teleportCount == byte.MaxValue)
                        {
                            teleportCount = 1;
                        }
                        else
                        {
                            teleportCount++;
                        }
                        localTeleportCount = teleportCount;
                    }
                }
                else
                {
                    localTeleportCount = teleportCount;
                }
            }
        }

        [HideInInspector]
        public bool localTransformFlag = true;
        [HideInInspector]
        public bool leftHandFlag;
        [HideInInspector]
        public bool kinematicFlag;
        [HideInInspector]
        public bool pickupableFlag = true;
        [HideInInspector]
        public bool bounceFlag;
        [HideInInspector]
        public bool sleepFlag = true;

        [HideInInspector]
        public float autoSmoothingTime;

        /*
            STATES
            Tells us what is going on with our object. Typically triggered by collider or pickup events.
            Negative state IDs are the built-in states.
            Positive or 0 state IDs are custom states
            Default state is -1, Teleport.
        */
        public const int STATE_PHYSICS = -1;//we lerp objects into place and then let physics take over on the next physics frame
        public const int STATE_HELD = -2;//we continually try to place an object in a player's left hand
        public const int STATE_LOCAL_TO_OWNER = -3;//we continually try to place an object local to a player's position and rotation
        public const int STATE_BONE = -4;//everything after this means the object is attached to an avatar's bones.
        public static string StateToStr(int s)
        {
            switch (s)
            {
                case STATE_PHYSICS:
                    {
                        return "STATE_PHYSICS";
                    }
                case STATE_HELD:
                    {
                        return "STATE_HELD";
                    }
                case STATE_LOCAL_TO_OWNER:
                    {
                        return "STATE_LOCAL_TO_OWNER";
                    }
                default:
                    {
                        if (s < 0)
                        {
                            HumanBodyBones bone = (HumanBodyBones)(STATE_BONE - s);
                            return "STATE_BONE: " + bone.ToString();
                        }
                        return "CUSTOM STATE: " + s.ToString();
                    }
            }
        }
        public virtual string prettyPrint()
        {
            return StateToStr(state) + " local:" + localTransformFlag + " left:" + leftHandFlag + " k:" + kinematicFlag + " p:" + pickupableFlag + " b:" + bounceFlag + " s:" + sleepFlag + " loop:" + loopTimingFlag + " pos:" + pos + " rot:" + rot + " vel:" + vel + " spin:" + spin;
        }


        public void IncrementSyncCounter()
        {
            if (syncCount == byte.MaxValue)
            {
                syncCount = 1;//can't ever be 0 again. So if it is 0 then we know no network syncs have gone through yet.
            }
            else
            {
                syncCount++;
            }
            localSyncCount = syncCount;
        }

        float remainingSmoothingTime;
        float lastSmoothingCalcTime;
        public void CalcSmoothingTime(float sendTime)
        {
            if (smoothingTime > 0)
            {
                autoSmoothingTime = smoothingTime;
            }
            else if (smoothingTime == 0)
            {
                autoSmoothingTime = Time.realtimeSinceStartup - Networking.SimulationTime(gameObject);
            }
            else
            {
                remainingSmoothingTime = -smoothingTime - (Time.realtimeSinceStartup - sendTime);
                if (autoSmoothingTime == 0)
                {
                    autoSmoothingTime = Mathf.Clamp(remainingSmoothingTime, 0, Time.realtimeSinceStartup - lastSmoothingCalcTime);
                }
                else
                {
                    autoSmoothingTime = Mathf.Lerp(autoSmoothingTime, Mathf.Clamp(remainingSmoothingTime, 0, Time.realtimeSinceStartup - lastSmoothingCalcTime), Mathf.Abs((remainingSmoothingTime - autoSmoothingTime) / smoothingTime));
                }
            }
            lastSmoothingCalcTime = Time.realtimeSinceStartup;
        }
        [System.NonSerialized]
        public VRCPlayerApi prevOwner;
        [System.NonSerialized, FieldChangeCallback(nameof(Owner))]
        public VRCPlayerApi _Owner;
        public VRCPlayerApi Owner
        {
            get => _Owner;
            set
            {
                if (_Owner != value)
                {
                    prevOwner = _Owner;
                    _Owner = value;
                    OnChangeOwner();
                    if (IsOwner())
                    {
                        if (state <= STATE_HELD && (pickup == null || !pickup.IsHeld))
                        {
                            state = STATE_PHYSICS;
                        }
                    }
                    else
                    {
                        if (pickup)
                        {
                            pickup.Drop();
                        }
                    }
                }
            }
        }
        public bool IsOwner()
        {
#if !COMPILER_UDONSHARP && UNITY_EDITOR
            return true;
#endif
            if (!Utilities.IsValid(Owner))
            {
                Owner = Networking.GetOwner(data.gameObject);
                return Owner.isLocal;
            }
            return Owner.isLocal;
        }

        public void Start()
        {
            Owner = Networking.GetOwner(data.gameObject);
            if (sleepOnSpawn && syncCount == 0)
            {
                rigid.Sleep();
            }
        }

        UdonBehaviour[] newBehaviourListeners;
        public void AddBehaviourListener(UdonBehaviour listener)
        {
            foreach (UdonBehaviour udon in _behaviourEventListeners)
            {
                if (udon == listener)
                {
                    return;
                }
            }
            newBehaviourListeners = new UdonBehaviour[_behaviourEventListeners.Length + 1];
            _behaviourEventListeners.CopyTo(newBehaviourListeners, 0);
            newBehaviourListeners[_behaviourEventListeners.Length] = listener;
            _behaviourEventListeners = newBehaviourListeners;
        }

        bool foundListener = false;
        public void RemoveBehaviourListener(UdonBehaviour listener)
        {
            if (_behaviourEventListeners.Length == 0)
            {
                return;
            }
            newBehaviourListeners = new UdonBehaviour[_behaviourEventListeners.Length - 1];
            foundListener = false;
            for (int i = 0; i < newBehaviourListeners.Length; i++)
            {
                if (_behaviourEventListeners[i] == listener)
                {
                    foundListener = true;
                }
                if (foundListener)
                {
                    newBehaviourListeners[i] = _behaviourEventListeners[i + 1];
                }
                else
                {
                    newBehaviourListeners[i] = _behaviourEventListeners[i];
                }
            }
            if (foundListener)
            {
                _behaviourEventListeners = newBehaviourListeners;
            }
        }

        LightSyncListener[] newClassListeners;
        public void AddClassListener(LightSyncListener listener)
        {
            foreach (LightSyncListener classListener in _classEventListeners)
            {
                if (classListener == listener)
                {
                    return;
                }
            }
            newClassListeners = new LightSyncListener[_classEventListeners.Length + 1];
            _classEventListeners.CopyTo(newBehaviourListeners, 0);
            newClassListeners[_classEventListeners.Length] = listener;
            _classEventListeners = newClassListeners;
        }

        public void RemoveClassListener(LightSyncListener listener)
        {
            if (_classEventListeners.Length == 0)
            {
                return;
            }
            newClassListeners = new LightSyncListener[_classEventListeners.Length - 1];
            foundListener = false;
            for (int i = 0; i < newClassListeners.Length; i++)
            {
                if (_classEventListeners[i] == listener)
                {
                    foundListener = true;
                }
                if (foundListener)
                {
                    newClassListeners[i] = _classEventListeners[i + 1];
                }
                else
                {
                    newClassListeners[i] = _classEventListeners[i];
                }
            }
            if (foundListener)
            {
                _classEventListeners = newClassListeners;
            }
        }

        public void DelayedRespawn(int delayFrames)
        {
            SendCustomEventDelayedFrames(nameof(RespawnIfNetworkNotClogged), delayFrames);
        }

        public void RespawnIfNetworkNotClogged()
        {
            if (Networking.IsClogged)
            {
                SendCustomEventDelayedFrames(nameof(RespawnIfNetworkNotClogged), Random.Range(1, 10));
            }
            else
            {
                Respawn();
            }
        }

        public void Respawn()
        {
            if (useWorldSpaceTransforms)
            {
                TeleportToWorldSpace(spawnPos, spawnRot, sleepOnSpawn);
            }
            else
            {
                TeleportToLocalSpace(spawnPos, spawnRot, sleepOnSpawn);
            }

            if (enterFirstCustomStateOnStart && customStates.Length > 0)
            {
                customStates[0].EnterState();
            }
        }

        public void TeleportToLocalSpace(Vector3 position, Quaternion rotation, bool shouldSleep)
        {
            TakeOwnershipIfNotOwner();
            transform.localPosition = position;
            transform.localRotation = rotation;
            state = STATE_PHYSICS;
            if (shouldSleep)
            {
                sleepFlag = true;
                StartEnsureSleep();
            }
            teleportFlag = true;
            Sync();
            if (shouldSleep)
            {
                //needs to record transforms early
                if (localTransformFlag)
                {
                    RecordLocalTransforms();
                }
                else
                {
                    RecordWorldTransforms();
                }
            }
        }

        public void TeleportToWorldSpace(Vector3 position, Quaternion rotation, bool shouldSleep)
        {
            TakeOwnershipIfNotOwner();
            transform.position = position;
            transform.rotation = rotation;
            state = STATE_PHYSICS;
            if (shouldSleep)
            {
                sleepFlag = true;
                StartEnsureSleep();
            }
            teleportFlag = true;
            Sync();
            if (shouldSleep)
            {
                //needs to record transforms early
                if (localTransformFlag)
                {
                    RecordLocalTransforms();
                }
                else
                {
                    RecordWorldTransforms();
                }
            }
        }

        public void Sleep()
        {
            rigid.Sleep();
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public void _print(string message)
        {
            if (!debugLogs)
            {
                return;
            }
            Debug.LogFormat(this, "[LightSync] {0}: {1}", name, message);
        }

        public void Sync()
        {
            TakeOwnershipIfNotOwner();
            StartLoop();
        }

        bool firstLoop = false;
        public void StartLoop()
        {
            firstLoop = true;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
            OnLerp(0, 0);
            data.SyncNewData();
            return;
#endif
            switch (loopTimingFlag)
            {
                case 0://Update
                    {
                        //Use When bone and avatar positions matter. The Official VRChat docs say that PostLateUpdate is better for this but the Official VRChat docs are fucking lying.
                        fixedLooper.StopLoop();
                        lateLooper.StopLoop();
                        looper.StartLoop();
                        break;
                    }
                case 1://FixedUpdate
                    {
                        //Use when physics matter
                        looper.StopLoop();
                        lateLooper.StopLoop();
                        fixedLooper.StartLoop();
                        break;
                    }
                default://PostLateUpdate
                    {
                        //Use everywhere else
                        looper.StopLoop();
                        fixedLooper.StopLoop();
                        lateLooper.StartLoop();
                        break;
                    }
            }
        }

        public void StopLoop()
        {
            fixedLooper.StopLoop();
            lateLooper.StopLoop();
            looper.StopLoop();
        }
        public void SyncIfOwner()
        {
            if (IsOwner())
            {
                StartLoop();
            }
        }
        [System.NonSerialized]
        public int lastCollision = -1001;
        [System.NonSerialized]
        public int lastTeleport = -1001;
        public void OnCollisionEnter(Collision other)
        {
            if (lastCollision == Time.frameCount || !syncCollisions || lastTeleport + 1 >= Time.frameCount)
            {
                return;
            }
            //decide if we should take ownership or not
            if (IsOwner() && takeOwnershipOfOtherObjectsOnCollision && Utilities.IsValid(other) && Utilities.IsValid(other.collider))
            {
                LightSync otherSync = other.collider.GetComponent<LightSync>();
                if (otherSync && otherSync.state == STATE_PHYSICS && otherSync.allowOthersToTakeOwnershipOnCollision && (!otherSync.takeOwnershipOfOtherObjectsOnCollision || otherSync.rigid.velocity.sqrMagnitude < rigid.velocity.sqrMagnitude))
                {
                    otherSync.TakeOwnershipIfNotOwner();
                }
            }
            OnCollision();
        }

        public void OnCollisionExit(Collision other)
        {
            if (lastCollision == Time.frameCount || !syncCollisions || lastTeleport + 1 >= Time.frameCount)
            {
                return;
            }
            OnCollision();
        }

        public void OnParticleCollision(GameObject other)
        {
            if (!syncParticleCollisions || other == gameObject || lastCollision == Time.frameCount || lastTeleport + 1 >= Time.frameCount)
            {
                return;
            }
            OnCollision();
        }

        public void OnCollision()
        {
            if (IsOwner())
            {
                if (state == STATE_PHYSICS)
                {
                    bounceFlag = true;
                    sleepFlag = rigid.isKinematic || rigid.IsSleeping();
                    Sync();
                }
            }
            // else if (!fixedLooper.enabled && minimumSleepFrames > 0 && sleepFlag && state == STATE_PHYSICS)
            // {
            //     RequestLocalDriftCheck();
            // }
            lastCollision = Time.frameCount;
        }

        bool driftCheckRequested = false;
        float lastLocalDriftCheckRequest;
        float localdriftcheckDelay;
        public void RequestLocalDriftCheck()
        {
            if (driftCheckRequested)
            {
                return;
            }
            driftCheckRequested = true;
            lastLocalDriftCheckRequest = Time.timeSinceLevelLoad;
            localdriftcheckDelay = Random.Range(2, 5) * (Time.realtimeSinceStartup - Networking.SimulationTime(gameObject));
            SendCustomEventDelayedSeconds(nameof(LocalDriftCheck), localdriftcheckDelay);
        }
        public void LocalDriftCheck()
        {
            driftCheckRequested = false;
            if (IsOwner() || state != STATE_PHYSICS)
            {
                return;
            }
            if (lastLocalDriftCheckRequest > data.lastDeserialization)
            {
                if (localTransformFlag)
                {
                    if (!LooseLocalTransformDrifted())
                    {
                        return;
                    }
                }
                else if (!LooseWorldTransformDrifted())
                {
                    return;
                }
                //replay the last message
                Debug.LogWarning("localdriftcheck");
                StartLoop();
            }
        }

        public bool LooseLocalTransformDrifted()
        {
            return Vector3.Distance(transform.localPosition, pos) > 0.25f || Quaternion.Angle(transform.localRotation, rot) > 3f;
        }

        public bool LooseWorldTransformDrifted()
        {
            return Vector3.Distance(rigid.position, pos) > 0.25f || Quaternion.Angle(rigid.rotation, rot) > 3f;
        }

        public void TakeOwnership()
        {
            Networking.SetOwner(Networking.LocalPlayer, data.gameObject);
        }

        public void TakeOwnershipIfNotOwner()
        {
#if !COMPILER_UDONSHARP && UNITY_EDITOR
            return;
#endif
            //use the native thing to make sure we get it right
            if (!IsOwner())
            {
                TakeOwnership();
            }
        }

        public override void OnPickup()
        {
            TakeOwnershipIfNotOwner();
            state = STATE_HELD;
            Sync();
        }

        public override void OnDrop()
        {
            if (IsOwner() && IsHeld && !pickup.IsHeld)
            {
                //was truly dropped. We didn't like switch hands or something.
                state = STATE_PHYSICS;
                Sync();
#if UNITY_EDITOR
                // Calculate throw velocity
                //Taken from Client Sim. Only here to fix client sim throwing
                float holdDuration = startHold < 0 ? 0 : Mathf.Clamp(Time.timeSinceLevelLoad - startHold, 0, 3);
                if (holdDuration > 0.2f)
                {
                    float power = holdDuration * 500 * pickup.ThrowVelocityBoostScale;
                    Vector3 throwForce = power * (Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward);
                    rigid.AddForce(throwForce);
                }
#endif
            }
        }

#if UNITY_EDITOR
        float startHold = -1001f;
        public override void InputDrop(bool value, UdonInputEventArgs args)
        {
            if (value)
            {
                startHold = Time.timeSinceLevelLoad;
            }
            else
            {
                startHold = -1001f;
            }
        }
#endif

        public bool IsHeld
        {
            get => state == STATE_HELD;
        }

        public bool IsAttachedToPlayer
        {
            get => state <= STATE_LOCAL_TO_OWNER;
        }

        public void OnEnable()
        {
            if (!data)
            {
                //happens when there's a weird race condition thingy
            }
            if (Owner != Networking.GetOwner(data.gameObject))
            {
                Owner = Networking.GetOwner(data.gameObject);
            }
            if (IsOwner())
            {
                if (!Networking.LocalPlayer.IsOwner(gameObject))
                {
                    Networking.SetOwner(Owner, gameObject);
                }
                LocalDriftCheck();
            }
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (separateHelperObjects && Utilities.IsValid(player) && player.isLocal)
            {
                TakeOwnershipIfNotOwner();
            }
        }

        public void OnChangeOwner()
        {
            //Gets called by data object
            foreach (LightSyncListener listener in _classEventListeners)
            {
                listener.OnChangeOwner(this, prevOwner, Owner);
            }

            foreach (UdonBehaviour behaviour in _behaviourEventListeners)
            {
                behaviour.SetProgramVariable<LightSync>(LightSyncListener.syncVariableName, this);
                behaviour.SetProgramVariable<VRCPlayerApi>(LightSyncListener.prevOwnerVariableName, prevOwner);
                behaviour.SetProgramVariable<VRCPlayerApi>(LightSyncListener.currentOwnerVariableName, Owner);
                behaviour.SendCustomEvent(LightSyncListener.changeOwnerEventName);
            }
        }

        public void ChangeState(sbyte newStateID)
        {
            TakeOwnershipIfNotOwner();
            state = newStateID;
            Sync();
        }

        [HideInInspector, SerializeField]
        bool lastKinematic;
        [HideInInspector, SerializeField]
        bool lastPickupable;
        public void OnEnterState()
        {
            if (state >= 0 && state < customStates.Length)
            {
                customStates[state].OnEnterState();
                return;
            }
            if (IsOwner())
            {
                RecordFlags();
            }
            else
            {
                ApplyFlags();
            }
            RecordTransforms();
            foreach (LightSyncListener listener in _classEventListeners)
            {
                listener.OnChangeState(this, prevState, state);
            }
            foreach (UdonBehaviour behaviour in _behaviourEventListeners)
            {
                behaviour.SetProgramVariable<LightSync>(LightSyncListener.syncVariableName, this);
                behaviour.SetProgramVariable<int>(LightSyncListener.prevStateVariableName, prevState);
                behaviour.SetProgramVariable<int>(LightSyncListener.prevStateVariableName, state);
                behaviour.SendCustomEvent(LightSyncListener.changeStateEventName);
            }
        }

        public void OnExitState()
        {
            if (prevState >= 0 && prevState < customStates.Length)
            {
                customStates[prevState].OnExitState();
                return;
            }
            if (prevState != STATE_PHYSICS)
            {
                ResetFlags();
            }
            if (pickup && prevState == STATE_HELD && !IsOwner())
            {
                pickup.Drop();
            }
            // if (!IsOwner() || prevState < STATE_PHYSICS)
            // {
            //     if (syncIsKinematic)
            //     {
            //         rigid.isKinematic = kinematicFlag;
            //     }
            //     else if ((prevState == STATE_HELD && kinematicWhileHeld) || ((prevState == STATE_BONE || prevState == STATE_LOCAL_TO_OWNER) && kinematicWhileAttachedToPlayer))
            //     {
            //         rigid.isKinematic = lastKinematic;
            //     }
            //     if (pickup)
            //     {
            //         if (state != STATE_HELD && state < 0)
            //         {
            //             pickup.Drop();
            //         }
            //         if (syncPickupable)
            //         {
            //             pickup.pickupable = pickupableFlag;
            //         }
            //         else
            //         {
            //             pickup.pickupable = lastPickupable;
            //         }
            //     }
            // }
        }

        public void RecordFlags()
        {
            if (syncIsKinematic)
            {
                kinematicFlag = lastKinematic;
            }
            if (syncPickupable)
            {
                pickupableFlag = lastPickupable;
            }

            switch (state)
            {
                case STATE_PHYSICS:
                    {
                        localTransformFlag = !useWorldSpaceTransforms;
                        sleepFlag = rigid.isKinematic || rigid.IsSleeping();
                        leftHandFlag = false;
                        bounceFlag = false;//determine if we need it later
                        loopTimingFlag = LOOP_FIXEDUPDATE;
                        break;
                    }
                case STATE_HELD:
                    {
                        if (controlPickupableState)
                        {
                            pickup.pickupable = allowTheftFromSelf;
                        }
                        rigid.isKinematic |= kinematicWhileHeld;
                        localTransformFlag = !useWorldSpaceTransformsWhenHeldOrAttachedToPlayer;
                        sleepFlag = rigid.IsSleeping();
                        leftHandFlag = pickup && (pickup.currentHand == VRC_Pickup.PickupHand.Left);
                        bounceFlag = false;
                        loopTimingFlag = LOOP_POSTLATEUPDATE;

                        if (leftHandFlag)
                        {
                            if (Owner.GetBonePosition(HumanBodyBones.LeftHand) == Vector3.zero)
                            {
                                localTransformFlag = false;
                            }
                        }
                        else if (Owner.GetBonePosition(HumanBodyBones.RightHand) == Vector3.zero)
                        {
                            localTransformFlag = false;
                        }
                        break;
                    }
                case STATE_LOCAL_TO_OWNER:
                    {
                        localTransformFlag = !useWorldSpaceTransformsWhenHeldOrAttachedToPlayer;
                        sleepFlag = rigid.IsSleeping();
                        leftHandFlag = false;
                        bounceFlag = false;
                        loopTimingFlag = LOOP_UPDATE;
                        rigid.isKinematic |= kinematicWhileAttachedToPlayer;
                        break;
                    }
                default:
                    {
                        localTransformFlag = !useWorldSpaceTransformsWhenHeldOrAttachedToPlayer;
                        sleepFlag = rigid.IsSleeping();
                        leftHandFlag = false;
                        bounceFlag = false;
                        loopTimingFlag = LOOP_UPDATE;
                        rigid.isKinematic |= kinematicWhileAttachedToPlayer;
                        break;
                    }
            }
        }

        public void ResetFlags()
        {
            if (pickup && controlPickupableState)
            {
                if (syncPickupable)
                {
                    pickup.pickupable = pickupableFlag;
                    lastPickupable = pickupableFlag;
                }
                else
                {
                    pickup.pickupable = lastPickupable;
                }
            }

            if (syncIsKinematic)
            {
                rigid.isKinematic = kinematicFlag;
                lastKinematic = kinematicFlag;
            }
            else
            {
                rigid.isKinematic = lastKinematic;
            }
        }

        public void ApplyFlags()
        {
            if (pickup && controlPickupableState)
            {
                if (syncPickupable)
                {
                    pickup.pickupable = pickupableFlag;
                }
                else
                {
                    pickup.pickupable = lastPickupable;
                }

                if (state == STATE_HELD)
                {
                    pickup.pickupable &= !pickup.DisallowTheft;
                }
                else if (IsAttachedToPlayer)
                {
                    pickup.pickupable &= allowTheftWhenAttachedToPlayer && lastPickupable;
                }
            }

            if (syncIsKinematic)
            {
                rigid.isKinematic = kinematicFlag;
            }
        }

        public float GetInterpolation()
        {
            switch (loopTimingFlag)
            {
                case 0://Update
                    {
                        return looper.GetAutoSmoothedInterpolation(Time.timeSinceLevelLoad - looper.startTime);
                    }
                case 1://FixedUpdate
                    {
                        return fixedLooper.GetAutoSmoothedInterpolation(Time.timeSinceLevelLoad - fixedLooper.startTime);
                    }
                default://PostLateUpdate
                    {
                        return lateLooper.GetAutoSmoothedInterpolation(Time.timeSinceLevelLoad - lateLooper.startTime);
                    }
            }
        }

        bool _continueBool;
        public bool OnLerp(float elapsedTime, float autoSmoothedLerp)
        {
            if (!Utilities.IsValid(Owner))
            {
                return true;
            }
            if (state >= 0 && state < customStates.Length)
            {
                _continueBool = customStates[state].OnLerp(elapsedTime, autoSmoothedLerp);
            }
            else
            {
                switch (state)
                {
                    case STATE_PHYSICS:
                        {
                            _continueBool = PhysicsLerp(elapsedTime, autoSmoothedLerp);
                            break;
                        }
                    case STATE_HELD:
                        {
                            _continueBool = HeldLerp(elapsedTime, autoSmoothedLerp);
                            break;
                        }
                    case STATE_LOCAL_TO_OWNER:
                        {
                            _continueBool = LocalLerp(elapsedTime, autoSmoothedLerp);
                            break;
                        }
                    default:
                        {
                            _continueBool = BoneLerp(elapsedTime, autoSmoothedLerp);
                            break;
                        }
                }
            }
            firstLoop = false;
            if (!_continueBool || autoSmoothedLerp >= 1.0f)
            {
                OnLerpEnd();
            }
            return _continueBool;
        }
        public void OnLerpEnd()
        {
            //Gets called by data object
            foreach (LightSyncListener listener in _classEventListeners)
            {
                listener.OnLerpEnd(this);
            }

            foreach (UdonBehaviour behaviour in _behaviourEventListeners)
            {
                behaviour.SetProgramVariable<LightSync>(LightSyncListener.syncVariableName, this);
                behaviour.SendCustomEvent(LightSyncListener.lerpStopEventName);
            }
        }

        bool shouldSync;
        Vector3 tempPos;
        Vector3 endVel;
        Quaternion tempRot;
        bool PhysicsLerp(float elapsedTime, float autoSmoothedLerp)
        {
            if (elapsedTime == 0)
            {
                if (localTransformFlag)
                {
                    RecordLocalTransforms();
                }
                else
                {
                    RecordWorldTransforms();
                }
            }

            if (IsOwner())
            {
                shouldSync = false;
                if (runEveryFrameOnOwner)
                {
                    shouldSync = TransformDrifted();
                }
                if (!rigid.isKinematic && sleepFlag != rigid.IsSleeping() && (lastEnsureSleep < 0 || lastEnsureSleep < Time.frameCount - 1))
                {
                    sleepFlag = rigid.IsSleeping();
                    shouldSync = true;
                }
                if (rigid.position.y < respawnHeight)
                {
                    Respawn();
                    return true;
                }
                if (shouldSync)
                {
                    Sync();
                }
                return shouldSync || runEveryFrameOnOwner || !rigid.IsSleeping() || (lastEnsureSleep >= 0 && lastEnsureSleep > Time.frameCount - 1);
            }
            else
            {
                if (bounceFlag)
                {
                    //don't smoothly lerp the velocity to simulate a bounce
                    endVel = recordedVel;
                }
                else
                {
                    endVel = recordedDestVel;
                }
                if (autoSmoothedLerp >= 1.0f)
                {
                    ApplyTransforms(pos, rot);
                    ApplyVelocities();
                    return false;
                }
                else
                {
                    tempPos = HermiteInterpolatePosition(recordedPos, recordedVel, recordedDestPos, endVel, autoSmoothedLerp, autoSmoothingTime);
                    tempRot = Quaternion.Slerp(recordedRot, recordedDestRot, autoSmoothedLerp);
                    ApplyTransforms(tempPos, tempRot);
                    if (!rigid.isKinematic)
                    {
                        rigid.velocity = Vector3.zero;
                        rigid.angularVelocity = Vector3.zero;
                    }
                }
                return true;
            }
        }

        Vector3 parentPos;
        Quaternion parentRot;
        bool HeldLerp(float elapsedTime, float autoSmoothedLerp)
        {
            if (localTransformFlag)
            {
                if (leftHandFlag)
                {
                    parentPos = Owner.GetBonePosition(HumanBodyBones.LeftHand);
                    parentRot = Owner.GetBoneRotation(HumanBodyBones.LeftHand);
                }
                else
                {
                    parentPos = Owner.GetBonePosition(HumanBodyBones.RightHand);
                    parentRot = Owner.GetBoneRotation(HumanBodyBones.RightHand);
                }
            }
            else
            {
                parentPos = Owner.GetPosition();
                parentRot = Owner.GetRotation();
            }

            if (elapsedTime == 0)
            {
                RecordRelativeTransforms(parentPos, parentRot);
            }

            if (IsOwner())
            {
                if (RelativeTransformDrifted(parentPos, parentRot))
                {
                    Sync();
                }
            }
            else
            {
                tempPos = HermiteInterpolatePosition(recordedPos, Vector3.zero, recordedDestPos, Vector3.zero, autoSmoothedLerp, autoSmoothingTime);
                tempRot = Quaternion.Slerp(recordedRot, recordedDestRot, autoSmoothedLerp);
                ApplyRelativeTransforms(parentPos, parentRot, tempPos, tempRot);
                if (!rigid.isKinematic)
                {
                    rigid.velocity = Vector3.zero;
                    rigid.angularVelocity = Vector3.zero;
                }
            }
            return true;
        }

        bool LocalLerp(float elapsedTime, float autoSmoothedLerp)
        {
            parentPos = Owner.GetPosition();
            parentRot = Owner.GetRotation();
            if (elapsedTime == 0)
            {
                RecordRelativeTransforms(parentPos, parentRot);
            }
            if (IsOwner())
            {
                if (runEveryFrameOnOwner && RelativeTransformDrifted(parentPos, parentRot))
                {
                    Sync();
                }
                ApplyRelativeTransforms(parentPos, parentRot, pos, rot);
            }
            else
            {
                tempPos = HermiteInterpolatePosition(recordedPos, Vector3.zero, recordedDestPos, Vector3.zero, autoSmoothedLerp, autoSmoothingTime);
                tempRot = Quaternion.Slerp(recordedRot, recordedDestRot, autoSmoothedLerp);
                ApplyRelativeTransforms(parentPos, parentRot, tempPos, tempRot);
            }
            if (!rigid.isKinematic)
            {
                rigid.velocity = Vector3.zero;
                rigid.angularVelocity = Vector3.zero;
            }
            return true;
        }

        bool BoneLerp(float elapsedTime, float autoSmoothedLerp)
        {
            if (localTransformFlag && state <= STATE_BONE && state > STATE_BONE - ((sbyte)HumanBodyBones.LastBone))
            {
                HumanBodyBones parentBone = (HumanBodyBones)(STATE_BONE - state);
                parentPos = Owner.GetBonePosition(parentBone);
                parentRot = Owner.GetBoneRotation(parentBone);
            }
            else
            {
                parentPos = Owner.GetPosition();
                parentRot = Owner.GetRotation();
            }
            if (elapsedTime == 0)
            {
                RecordRelativeTransforms(parentPos, parentRot);
            }
            if (IsOwner())
            {
                if (runEveryFrameOnOwner && RelativeTransformDrifted(parentPos, parentRot))
                {
                    Sync();
                }
                ApplyRelativeTransforms(parentPos, parentRot, pos, rot);
            }
            else
            {
                tempPos = HermiteInterpolatePosition(recordedPos, Vector3.zero, recordedDestPos, Vector3.zero, autoSmoothedLerp, autoSmoothingTime);
                tempRot = Quaternion.Slerp(recordedRot, recordedDestRot, autoSmoothedLerp);
                ApplyRelativeTransforms(parentPos, parentRot, tempPos, tempRot);
            }
            if (!rigid.isKinematic)
            {
                rigid.velocity = Vector3.zero;
                rigid.angularVelocity = Vector3.zero;
            }
            return true;
        }

        //Helper functions
        Vector3 recordedPos;
        Quaternion recordedRot;
        Vector3 recordedVel;
        Vector3 recordedSpin;
        Vector3 recordedDestPos;
        Quaternion recordedDestRot;
        Vector3 recordedDestVel;
        Vector3 recordedDestSpin;
        public void RecordTransforms()
        {
            switch (state)
            {
                case STATE_PHYSICS:
                    {
                        if (localTransformFlag)
                        {
                            RecordLocalTransforms();
                        }
                        else
                        {
                            RecordWorldTransforms();
                        }
                        break;
                    }
                case STATE_HELD:
                    {
                        Vector3 parentPos = Vector3.zero;
                        Quaternion parentRot = Quaternion.identity;
                        if (localTransformFlag)
                        {
                            if (leftHandFlag)
                            {
                                parentPos = Owner.GetBonePosition(HumanBodyBones.LeftHand);
                                parentRot = Owner.GetBoneRotation(HumanBodyBones.LeftHand);
                            }
                            else
                            {
                                parentPos = Owner.GetBonePosition(HumanBodyBones.RightHand);
                                parentRot = Owner.GetBoneRotation(HumanBodyBones.RightHand);
                            }
                        }

                        if (!localTransformFlag || parentPos == Vector3.zero)
                        {
                            parentPos = Owner.GetPosition();
                            parentRot = Owner.GetRotation();
                        }
                        RecordRelativeTransforms(parentPos, parentRot);
                        break;
                    }
                case STATE_LOCAL_TO_OWNER:
                    {
                        Vector3 parentPos = Owner.GetPosition();
                        Quaternion parentRot = Owner.GetRotation();
                        RecordRelativeTransforms(parentPos, parentRot);
                        break;
                    }
                default:
                    {
                        Vector3 parentPos;
                        Quaternion parentRot;
                        if (localTransformFlag && state <= STATE_BONE && state > STATE_BONE - ((sbyte)HumanBodyBones.LastBone))
                        {
                            HumanBodyBones parentBone = (HumanBodyBones)(STATE_BONE - state);
                            parentPos = Owner.GetBonePosition(parentBone);
                            parentRot = Owner.GetBoneRotation(parentBone);
                        }
                        else
                        {
                            parentPos = Owner.GetPosition();
                            parentRot = Owner.GetRotation();
                        }
                        RecordRelativeTransforms(parentPos, parentRot);
                        break;
                    }
            }
        }

        public void RecordWorldTransforms()
        {
            recordedPos = rigid.position;
            recordedRot = rigid.rotation;
            recordedVel = rigid.velocity;
            recordedSpin = rigid.angularVelocity;
            if (IsOwner())
            {
                pos = recordedPos;
                rot = recordedRot;
                vel = recordedVel;
                spin = recordedSpin;
            }
            recordedDestPos = pos;
            recordedDestRot = rot;
            recordedDestVel = vel;
            recordedDestSpin = spin;
        }

        public void RecordLocalTransforms()
        {
            recordedPos = transform.localPosition;
            recordedRot = transform.localRotation;
            recordedVel = Quaternion.Inverse(rigid.rotation) * rigid.velocity;
            recordedSpin = Quaternion.Inverse(rigid.rotation) * rigid.angularVelocity;
            if (IsOwner())
            {
                pos = recordedPos;
                rot = recordedRot;
                vel = recordedVel;
                spin = recordedSpin;
            }
            recordedDestPos = pos;
            recordedDestRot = rot;
            recordedDestVel = vel;
            recordedDestSpin = spin;
        }

        public void RecordRelativeTransforms(Vector3 parentPos, Quaternion parentRot)
        {
            var invParentRot = Quaternion.Inverse(parentRot);
            recordedPos = invParentRot * (rigid.position - parentPos);
            recordedRot = invParentRot * rigid.rotation;
            recordedVel = Quaternion.Inverse(rigid.rotation) * rigid.velocity;
            recordedSpin = Quaternion.Inverse(rigid.rotation) * rigid.angularVelocity;
            if (IsOwner())
            {
                pos = recordedPos;
                rot = recordedRot;
                vel = recordedVel;
                spin = recordedSpin;
            }
            recordedDestPos = pos;
            recordedDestRot = rot;
            recordedDestVel = vel;
            recordedDestSpin = spin;
        }

        public void ApplyTransforms(Vector3 targetPos, Quaternion targetRot)
        {
            if (localTransformFlag)
            {
                transform.localPosition = targetPos;
                transform.localRotation = targetRot;
            }
            else
            {
                transform.position = targetPos;
                transform.rotation = targetRot;
            }
        }

        public void ApplyRelativeTransforms(Vector3 parentPos, Quaternion parentRot, Vector3 targetPos, Quaternion targetRot)
        {
            transform.position = parentPos + (parentRot * targetPos);
            transform.rotation = parentRot * targetRot;
        }

        public void ApplyVelocities()
        {
            if (rigid.isKinematic)
            {
                return;
            }
            if (sleepFlag)
            {
                StartEnsureSleep();
            }
            else if (localTransformFlag)
            {
                ApplyLocalVelocities();
            }
            else
            {
                ApplyWorldVelocities();
            }
        }

        int lastEnsureSleep = -1001;
        int sleepCount = 0;
        public void EnsureSleep()
        {
            if (lastEnsureSleep == Time.frameCount)
            {
                return;
            }
            if (sleepFlag && state == STATE_PHYSICS)
            {
                if (!rigid.IsSleeping())
                {
                    if (TransformDrifted())
                    {
                        ApplyTransforms(pos, rot);
                    }
                    rigid.Sleep();
                    SendCustomEventDelayedFrames(nameof(EnsureSleep), 0);
                    sleepCount = 0;
                }
                else
                {
                    sleepCount++;
                    lastEnsureSleep = Time.frameCount;
                    if (sleepCount >= minimumSleepFrames || minimumSleepFrames <= 0)
                    {
                        sleepCount = 0;
                    }
                    else
                    {
                        SendCustomEventDelayedFrames(nameof(EnsureSleep), 1);
                    }
                }
            }
        }

        public void StartEnsureSleep()
        {
            rigid.Sleep();
            lastEnsureSleep = Time.frameCount - 1;
            SendCustomEventDelayedFrames(nameof(EnsureSleep), 1);
        }

        public void ApplyWorldVelocities()
        {
            rigid.velocity = vel;
            rigid.angularVelocity = spin;
        }

        public void ApplyLocalVelocities()
        {
            rigid.velocity = transform.rotation * vel;
            rigid.angularVelocity = transform.rotation * spin;
        }

        float lastDriftCheck = -1001f;
        public bool TransformDrifted()
        {
            if (Time.timeSinceLevelLoad - Mathf.Max(looper.startTime, lastDriftCheck) > autoSmoothingTime)
            {
                return false;
            }
            lastDriftCheck = Time.timeSinceLevelLoad;
            if (localTransformFlag)
            {
                return LocalTransformDrifted();
            }
            return WorldTransformDrifted();
        }

        public bool LocalTransformDrifted()
        {
            return transform.localPosition != pos || Quaternion.Angle(transform.localRotation, rot) > 0.1f;
        }

        public bool WorldTransformDrifted()
        {
            return rigid.position != pos || Quaternion.Angle(rigid.rotation, rot) > 0.1f;
        }

        public bool RelativeTransformDrifted(Vector3 parentPos, Quaternion parentRot)
        {
            var invParentRot = Quaternion.Inverse(parentRot);
            var targetPos = invParentRot * (rigid.position - parentPos);
            var targetRot = invParentRot * rigid.rotation;
            return (Vector3.Distance(targetPos, pos) > positionDesyncThreshold) || (Quaternion.Angle(targetRot, rot) > rotationDesyncThreshold);
        }

        Vector3 posControl1;
        Vector3 posControl2;
        public Vector3 HermiteInterpolatePosition(Vector3 startPos, Vector3 startVel, Vector3 endPos, Vector3 endVel, float interpolation, float duration)
        {//Shout out to Kit Kat for suggesting the improved hermite interpolation
            if (interpolation >= 1)
            {
                return endPos;
            }
            posControl1 = startPos + startVel * duration * interpolation / 3f;
            posControl2 = endPos - endVel * duration * (1.0f - interpolation) / 3f;
            return Vector3.Lerp(Vector3.Lerp(posControl1, endPos, interpolation), Vector3.Lerp(startPos, posControl2, interpolation), interpolation);
        }

        //IGNORE
        //These are here to prevent some weird unity editor errors from clogging the logs
        [HideInInspector]
        public bool _showInternalObjects = true;

        [HideInInspector]
        public bool showInternalObjects = false;

        [HideInInspector]
        public bool separateHelperObjects = false;
#if UNITY_EDITOR && !COMPILER_UDONSHARP


        public void Reset()
        {
            id = (uint)GameObject.FindObjectsOfType(typeof(LightSync)).Length;
            AutoSetup();
            respawnHeight = VRC_SceneDescriptor.Instance.RespawnHeightY;
        }

        public void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }
            if (showInternalObjects)
            {
                if (data == null || separateHelperObjects == (data.transform == transform))
                {
                    CreateDataObject();
                }
                if (looper == null || separateHelperObjects == (looper.transform == transform))
                {
                    CreateLooperObject();
                }
            }
            if (_showInternalObjects != showInternalObjects)
            {
                // RefreshHideFlags();
                Invoke(nameof(RefreshHideFlags), 0f);
            }
            if (enterFirstCustomStateOnStart && customStates.Length > 0)
            {
                _state = customStates[0].stateID;
            }
        }

        public void RefreshHideFlags()
        {
            if (!data || !looper || !fixedLooper || !lateLooper)
            {
                return;
            }
            else
            {
                _showInternalObjects = showInternalObjects;
                data.RefreshHideFlags();
                looper.RefreshHideFlags();
                fixedLooper.RefreshHideFlags();
                lateLooper.RefreshHideFlags();
                foreach (var state in customStates)
                {
                    if (state is LightSyncStateWithData dataState)
                    {
                        dataState.RefreshFlags();
                    }
                }
                foreach (var enhancement in GetComponents<LightSyncEnhancementWithData>())
                {
                    enhancement.RefreshFlags();
                }
            }
        }

        public void HideUdonBehaviours()
        {
            var udons = GetComponents<UdonBehaviour>();
            foreach (var udon in udons)
            {
                if (UdonSharpEditorUtility.IsUdonSharpBehaviour(udon))
                {
                    udon.hideFlags |= HideFlags.HideInInspector;
                }
            }
        }

        public void ForceSetup()
        {
            DestroyInternalObjectsAsync();
            AutoSetup();
        }

        public void AutoSetup()
        {
            Debug.Log("LightSync Autosetup: " + name);
            if (!rigid || rigid.gameObject != gameObject)
            {
                rigid = GetComponent<Rigidbody>();
            }
            if (!pickup || pickup.gameObject != gameObject)
            {
                pickup = GetComponent<VRC_Pickup>();
            }
            lastPickupable = pickup && pickup.pickupable;
            lastKinematic = rigid.isKinematic;
            CreateDataObject();
            CreateLooperObject();
            SetupStates();
            SetupEnhancements();
            Sortlisteners();
            // Invoke(nameof(RefreshHideFlags), 0f);

            // if (!separateHelperObjects)
            // {
            //     foreach (var udon in GetComponents<UdonBehaviour>())
            //     {
            //         udon.SyncMethod = Networking.SyncType.Manual;
            //         var serializedUdon = new SerializedObject(udon);
            //         serializedUdon.Update();
            //         PrefabUtility.RecordPrefabInstancePropertyModifications(udon);
            //     }
            // }

            //save all the parameters for the first frame
            if (useWorldSpaceTransforms)
            {
                spawnPos = rigid.position;
                spawnRot = rigid.rotation;
            }
            else
            {
                spawnPos = transform.localPosition;
                spawnRot = transform.localRotation;
            }
            kinematicFlag = rigid.isKinematic;
            bounceFlag = false;
            pickupableFlag = pickup && pickup.pickupable;
            sleepFlag = sleepOnSpawn;
            if (enterFirstCustomStateOnStart && customStates.Length > 0)
            {
                customStates[0].EnterState();
            }
            else
            {
                state = STATE_PHYSICS;
            }
            data.SyncNewData();
            var serializedSync = new SerializedObject(this);
            var serializedData = new SerializedObject(data);
            var serializedLooper = new SerializedObject(looper);
            var serializedLateLooper = new SerializedObject(lateLooper);
            var serializedFixedLooper = new SerializedObject(fixedLooper);
            serializedSync.Update();
            serializedData.Update();
            serializedLooper.Update();
            serializedLateLooper.Update();
            serializedFixedLooper.Update();
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            PrefabUtility.RecordPrefabInstancePropertyModifications(looper);
            PrefabUtility.RecordPrefabInstancePropertyModifications(lateLooper);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fixedLooper);
            // data.RefreshHideFlags();
            // looper.RefreshHideFlags();
            // fixedLooper.RefreshHideFlags();
            // lateLooper.RefreshHideFlags();
            HideUdonBehaviours();
            EditorUtility.SetDirty(this);
        }

        public void SetupStates()
        {
            customStates = GetComponents<LightSyncState>();
            if (customStates.Length >= sbyte.MaxValue)
            {
                Debug.LogError("lol");
            }
            for (int i = 0; i < customStates.Length; i++)
            {
                customStates[i].stateID = (sbyte)i;
                customStates[i].sync = this;
                customStates[i].data = data;
                customStates[i].AutoSetup();
                var serialized = new SerializedObject(customStates[i]);
                serialized.Update();
                PrefabUtility.RecordPrefabInstancePropertyModifications(customStates[i]);
            }
        }

        public void SetupEnhancements()
        {
            foreach (var enhancement in GetComponents<LightSyncEnhancement>())
            {
                enhancement.sync = this;
                enhancement.AutoSetup();
                var serialized = new SerializedObject(enhancement);
                serialized.Update();
                PrefabUtility.RecordPrefabInstancePropertyModifications(enhancement);
            }
        }

        public void Sortlisteners()
        {
            eventListeners = eventListeners.Where(obj => Utilities.IsValid(obj)).Distinct().ToArray();
            System.Collections.Generic.List<LightSyncListener> classListeners = new();
            System.Collections.Generic.List<UdonBehaviour> behaviourListeners = new();
            LightSyncListener classListener;
            UdonBehaviour behaviourListener;
            foreach (Component listener in eventListeners)
            {
                classListener = listener as LightSyncListener;
                behaviourListener = listener as UdonBehaviour;
                if (classListener)
                {
                    classListeners.Add(classListener);
                }
                else if (behaviourListener)
                {
                    behaviourListeners.Add(behaviourListener);
                }
                var serialized = new SerializedObject(listener);
                serialized.Update();
                PrefabUtility.RecordPrefabInstancePropertyModifications(listener);
            }
            _classEventListeners = classListeners.ToArray();
            _behaviourEventListeners = behaviourListeners.ToArray();
        }

        public void CreateDataObject()
        {
            if (data != null)
            {
                if (data.sync != this)
                {
                    data.sync = this;
                }

                var shouldDelete = false;
                if (networkDataOptimization == NetworkDataOptimization.Ultra && data is not LightSyncDataUltra)
                {
                    shouldDelete = true;
                }
                else if (networkDataOptimization == NetworkDataOptimization.High && data is not LightSyncDataHigh)
                {
                    shouldDelete = true;
                }
                else if (networkDataOptimization == NetworkDataOptimization.Low && data is not LightSyncDataLow)
                {
                    shouldDelete = true;
                }
                else if (networkDataOptimization == NetworkDataOptimization.Unoptimized && data is not LightSyncDataUnoptimized)
                {
                    shouldDelete = true;
                }
                else if (separateHelperObjects == (data.transform == transform))
                {
                    shouldDelete = true;
                }

                if (!shouldDelete)
                {
                    return;
                }

                data.DestroyAsync();
            }
            GameObject dataObject;
            if (separateHelperObjects)
            {
                switch (networkDataOptimization)
                {
                    case NetworkDataOptimization.Ultra:
                        {
                            dataObject = new(name + "_dataUltra" + GUID.Generate());
                            break;
                        }
                    case NetworkDataOptimization.High:
                        {
                            dataObject = new(name + "_dataHigh" + GUID.Generate());
                            break;
                        }
                    case NetworkDataOptimization.Low:
                        {
                            dataObject = new(name + "_dataLow" + GUID.Generate());
                            break;
                        }
                    case NetworkDataOptimization.Unoptimized:
                        {
                            dataObject = new(name + "_dataUnoptimized" + GUID.Generate());
                            break;
                        }
                    default:
                        {
                            dataObject = new(name + "_dataDisabled " + GUID.Generate());
                            break;
                        }
                }
                dataObject.transform.SetParent(null, false);
            }
            else
            {
                dataObject = gameObject;
                Invoke(nameof(ForceSyncMethod), 0f);
            }
            switch (networkDataOptimization)
            {
                case NetworkDataOptimization.Ultra:
                    {
                        data = UdonSharpComponentExtensions.AddUdonSharpComponent<LightSyncDataUltra>(dataObject);
                        break;
                    }
                case NetworkDataOptimization.High:
                    {
                        data = UdonSharpComponentExtensions.AddUdonSharpComponent<LightSyncDataHigh>(dataObject);
                        break;
                    }
                case NetworkDataOptimization.Low:
                    {
                        data = UdonSharpComponentExtensions.AddUdonSharpComponent<LightSyncDataLow>(dataObject);
                        break;
                    }
                case NetworkDataOptimization.Unoptimized:
                    {
                        data = UdonSharpComponentExtensions.AddUdonSharpComponent<LightSyncDataUnoptimized>(dataObject);
                        break;
                    }
                default:
                    {
                        data = UdonSharpComponentExtensions.AddUdonSharpComponent<LightSyncDataDisabled>(dataObject);
                        break;
                    }
            }
            if (separateHelperObjects)
            {
                data.hideFlags &= ~HideFlags.HideInInspector;
            }
            else
            {
                data.hideFlags |= HideFlags.HideInInspector;
            }
            data.sync = this;
            // data.RefreshHideFlags();
        }

        public void ForceSyncMethod()
        {
            foreach (var udon in GetComponents<UdonBehaviour>())
            {
                udon.SyncMethod = Networking.SyncType.Manual;
            }
            Invoke(nameof(HideUdonBehaviours), 0f);
        }

        public void CreateLooperObject()
        {
            if (!data)
            {
                Debug.LogError("Can't create looper without data");
                return;
            }
            GameObject looperObject = data.gameObject;
            if (looper != null)
            {
                if (looper.gameObject != data.gameObject)
                {
                    looper.DestroyAsync();
                    if (fixedLooper)
                    {
                        fixedLooper.DestroyAsync();
                    }
                    if (lateLooper)
                    {
                        lateLooper.DestroyAsync();
                    }
                }
            }
            if (looper == null || looper.destroyCalled)
            {
                looper = UdonSharpComponentExtensions.AddUdonSharpComponent<LightSyncLooperUpdate>(looperObject);
            }
            looper.sync = this;
            // looper.RefreshHideFlags();
            looper.StopLoop();

            if (fixedLooper == null)
            {
                fixedLooper = looper.GetComponent<LightSyncLooperFixedUpdate>();
                if (fixedLooper == null || fixedLooper.destroyCalled)
                {
                    fixedLooper = UdonSharpComponentExtensions.AddUdonSharpComponent<LightSyncLooperFixedUpdate>(looperObject);
                }
            }
            fixedLooper.sync = this;
            // fixedLooper.RefreshHideFlags();
            fixedLooper.StopLoop();

            if (lateLooper == null)
            {
                lateLooper = looper.GetComponent<LightSyncLooperPostLateUpdate>();
                if (lateLooper == null || lateLooper.destroyCalled)
                {
                    lateLooper = UdonSharpComponentExtensions.AddUdonSharpComponent<LightSyncLooperPostLateUpdate>(looperObject);
                }
            }
            lateLooper.sync = this;
            // lateLooper.RefreshHideFlags();
            lateLooper.StopLoop();
            Invoke(nameof(HideUdonBehaviours), 0f);
        }

        public void OnDestroy()
        {
            DestroyInternalObjectsAsync();
        }

        public void DestroyInternalObjectsAsync()
        {
            Debug.LogWarning("DESTROY INTERNAL OBJECTS: " + name);
            if (data)
            {
                data.DestroyAsync();
                data = null;
            }
            if (looper)
            {
                looper.DestroyAsync();
                looper = null;
            }
            if (fixedLooper)
            {
                fixedLooper.DestroyAsync();
                fixedLooper = null;
            }
            if (lateLooper)
            {
                lateLooper.DestroyAsync();
                lateLooper = null;
            }
            foreach (var state in customStates)
            {
                if (state is LightSyncStateWithData dataState)
                {
                    dataState.DestroyInternalObjectsAsync();
                }
            }

            foreach (var enhancement in GetComponents<LightSyncEnhancementWithData>())
            {
                if (enhancement is LightSyncEnhancementWithData dataState)
                {
                    dataState.DestroyInternalObjectsAsync();
                }
            }
        }
#endif
    }
}
