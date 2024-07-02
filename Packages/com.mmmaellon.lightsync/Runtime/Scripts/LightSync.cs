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
        public byte localTeleportCount; //counts up every time we receive new data;
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
                    }
                }
                else
                {
                    if (IsOwner())
                    {
                        teleportCount = localTeleportCount;
                    }
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
            return Utilities.IsValid(Owner) && Owner.isLocal;
        }

        public void Start()
        {
            Owner = Networking.GetOwner(gameObject);
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
                rigid.Sleep();
            }
            teleportFlag = true;
            Sync();
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
                rigid.Sleep();
            }
            teleportFlag = true;
            Sync();
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
        public void OnCollisionEnter(Collision other)
        {
            if (lastCollision == Time.frameCount || !syncCollisions)
            {
                return;
            }
            //decide if we should take ownership or not
            if (IsOwner() && takeOwnershipOfOtherObjectsOnCollision && Utilities.IsValid(other) && Utilities.IsValid(other.collider))
            {
                LightSync otherSync = other.collider.GetComponent<LightSync>();
                if (otherSync && otherSync.state == STATE_PHYSICS && !otherSync.IsOwner() && otherSync.allowOthersToTakeOwnershipOnCollision && (!otherSync.takeOwnershipOfOtherObjectsOnCollision || otherSync.rigid.velocity.sqrMagnitude < rigid.velocity.sqrMagnitude))
                {
                    Networking.SetOwner(Networking.LocalPlayer, otherSync.gameObject);
                }
            }
            OnCollision();
        }

        public void OnCollisionExit(Collision other)
        {
            if (lastCollision == Time.frameCount || !syncCollisions)
            {
                return;
            }
            OnCollision();
        }

        public void OnParticleCollision(GameObject other)
        {
            if (!syncParticleCollisions || other == gameObject || lastCollision == Time.frameCount)
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
                    Sync();
                }
            }
            else if (sleepCount <= 0 && minimumSleepFrames > 0 && sleepFlag && state == STATE_PHYSICS)
            {
                EnsureSleep();
            }
            lastCollision = Time.frameCount;
        }

        public void TakeOwnership()
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }

        public void TakeOwnershipIfNotOwner()
        {
            if (!IsOwner())
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
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

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && player.isLocal)
            {
                Networking.SetOwner(player, data.gameObject);
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
                switch (state)
                {
                    case STATE_PHYSICS:
                        {
                            kinematicFlag = rigid.isKinematic;
                            pickupableFlag = pickup && pickup.pickupable;
                            localTransformFlag = !useWorldSpaceTransforms;
                            sleepFlag = rigid.IsSleeping();
                            leftHandFlag = false;
                            bounceFlag = false;//determine if we need it later
                            loopTimingFlag = LOOP_FIXEDUPDATE;
                            break;
                        }
                    case STATE_HELD:
                        {
                            if (prevState != state)
                            {
                                kinematicFlag = rigid.isKinematic;
                                pickupableFlag = pickup && pickup.pickupable;
                                lastPickupable = pickup.pickupable;
                                pickup.pickupable = allowTheftFromSelf;
                                lastKinematic = rigid.isKinematic;
                                rigid.isKinematic = kinematicWhileHeld;
                            }
                            localTransformFlag = !useWorldSpaceTransformsWhenHeldOrAttachedToPlayer;
                            sleepFlag = rigid.IsSleeping();
                            leftHandFlag = pickup.currentHand == VRC_Pickup.PickupHand.Left;
                            bounceFlag = true;
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
                            kinematicFlag = rigid.isKinematic;
                            sleepFlag = rigid.IsSleeping();
                            pickupableFlag = pickup && pickup.pickupable;
                            leftHandFlag = false;
                            bounceFlag = true;
                            loopTimingFlag = LOOP_UPDATE;
                            if (syncIsKinematic)
                            {
                                rigid.isKinematic = kinematicFlag || kinematicWhileAttachedToPlayer;
                            }
                            else
                            {
                                lastKinematic = rigid.isKinematic;
                                rigid.isKinematic = kinematicWhileAttachedToPlayer;
                            }
                            break;
                        }
                    default:
                        {
                            localTransformFlag = !useWorldSpaceTransformsWhenHeldOrAttachedToPlayer;
                            kinematicFlag = rigid.isKinematic;
                            sleepFlag = rigid.IsSleeping();
                            pickupableFlag = pickup && pickup.pickupable;
                            leftHandFlag = false;
                            bounceFlag = true;
                            loopTimingFlag = LOOP_UPDATE;
                            if (syncIsKinematic)
                            {
                                rigid.isKinematic = kinematicFlag || kinematicWhileAttachedToPlayer;
                            }
                            else
                            {
                                lastKinematic = rigid.isKinematic;
                                rigid.isKinematic = kinematicWhileAttachedToPlayer;
                            }
                            break;
                        }
                }
            }
            else
            {
                switch (state)
                {
                    case STATE_PHYSICS:
                        {
                            if (syncIsKinematic)
                            {
                                rigid.isKinematic = kinematicFlag;
                            }
                            if (syncPickupable && pickup)
                            {
                                pickup.pickupable = pickupableFlag;
                            }
                            break;
                        }
                    case STATE_HELD:
                        {
                            if (syncIsKinematic)
                            {
                                rigid.isKinematic = kinematicFlag || kinematicWhileHeld;
                            }
                            else
                            {
                                lastKinematic = rigid.isKinematic;
                                rigid.isKinematic = kinematicWhileHeld;
                            }
                            if (syncPickupable && pickup)
                            {
                                if (syncPickupable)
                                {
                                    pickup.pickupable = pickupableFlag && !pickup.DisallowTheft;
                                }
                                else
                                {
                                    lastPickupable = pickup.pickupable;
                                    pickup.pickupable = !pickup.DisallowTheft;
                                }
                            }
                            break;
                        }
                    case STATE_LOCAL_TO_OWNER:
                        {
                            if (syncIsKinematic)
                            {
                                rigid.isKinematic = kinematicFlag || kinematicWhileAttachedToPlayer;
                            }
                            else
                            {
                                lastKinematic = rigid.isKinematic;
                                rigid.isKinematic = kinematicWhileAttachedToPlayer;
                            }

                            if (syncPickupable && pickup)
                            {
                                if (syncPickupable)
                                {
                                    pickup.pickupable = pickupableFlag && allowTheftWhenAttachedToPlayer;
                                }
                                else
                                {
                                    lastPickupable = pickup.pickupable;
                                    pickup.pickupable = allowTheftWhenAttachedToPlayer;
                                }
                            }
                            break;
                        }

                    default:
                        {
                            if (syncIsKinematic)
                            {
                                rigid.isKinematic = kinematicFlag || kinematicWhileAttachedToPlayer;
                            }
                            else
                            {
                                lastKinematic = rigid.isKinematic;
                                rigid.isKinematic = kinematicWhileAttachedToPlayer;
                            }

                            if (syncPickupable && pickup)
                            {
                                if (syncPickupable)
                                {
                                    pickup.pickupable = pickupableFlag && allowTheftWhenAttachedToPlayer;
                                }
                                else
                                {
                                    lastPickupable = pickup.pickupable;
                                    pickup.pickupable = allowTheftWhenAttachedToPlayer;
                                }
                            }
                            break;
                        }
                }

            }
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
            if (!IsOwner() || prevState < STATE_PHYSICS)
            {
                if (syncIsKinematic)
                {
                    rigid.isKinematic = kinematicFlag;
                }
                else
                {
                    rigid.isKinematic = lastKinematic;
                }
                if (pickup)
                {
                    if (state != STATE_HELD && state < 0)
                    {
                        pickup.Drop();
                    }
                    if (syncPickupable)
                    {
                        pickup.pickupable = pickupableFlag;
                    }
                    else
                    {
                        pickup.pickupable = lastPickupable;
                    }
                }
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
            if (firstLoop && (!_continueBool || autoSmoothedLerp >= 1.0f))
            {
                OnLerpEnd();
                firstLoop = false;
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
        Vector3 startVel;
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
                if (syncIsKinematic && (kinematicFlag != rigid.isKinematic))
                {
                    kinematicFlag = rigid.isKinematic;
                    shouldSync = true;
                }
                else if (syncPickupable && pickup && (pickupableFlag != pickup.pickupable))
                {
                    pickupableFlag = pickup.pickupable;
                    shouldSync = true;
                }
                else if (sleepFlag != rigid.IsSleeping())
                {
                    sleepFlag = rigid.IsSleeping();
                    shouldSync = true;
                }
                else if (rigid.position.y < respawnHeight)
                {
                    Respawn();
                    return true;
                }
                if (shouldSync)
                {
                    Sync();
                }
                return shouldSync || runEveryFrameOnOwner || !rigid.IsSleeping();
            }
            else
            {
                if (bounceFlag)
                {
                    //don't smoothly lerp the velocity to simulate a bounce
                    endVel = startVel;
                }
                else
                {
                    endVel = vel;
                }
                tempPos = HermiteInterpolatePosition(recordedPos, startVel, pos, endVel, autoSmoothedLerp, autoSmoothingTime);
                tempRot = Quaternion.Slerp(recordedRot, rot, autoSmoothedLerp);
                ApplyTransforms(tempPos, tempRot);
                if (autoSmoothedLerp >= 1.0f)
                {
                    ApplyVelocities();
                    return false;
                }
                if (rigid.isKinematic)
                {
                    rigid.velocity = Vector3.zero;
                    rigid.angularVelocity = Vector3.zero;
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
                tempPos = HermiteInterpolatePosition(recordedPos, Vector3.zero, pos, Vector3.zero, autoSmoothedLerp, autoSmoothingTime);
                tempRot = Quaternion.Slerp(recordedRot, rot, autoSmoothedLerp);
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
                tempPos = HermiteInterpolatePosition(recordedPos, Vector3.zero, pos, Vector3.zero, autoSmoothedLerp, autoSmoothingTime);
                tempRot = Quaternion.Slerp(recordedRot, rot, autoSmoothedLerp);
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
                tempPos = HermiteInterpolatePosition(recordedPos, Vector3.zero, pos, Vector3.zero, autoSmoothedLerp, autoSmoothingTime);
                tempRot = Quaternion.Slerp(recordedRot, rot, autoSmoothedLerp);
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
        // public void RecordTransforms()
        // {
        //     switch (state)
        //     {
        //         case STATE_PHYSICS:
        //             {
        //                 if (localTransformFlag)
        //                 {
        //                     RecordLocalTransforms();
        //                 }
        //                 else
        //                 {
        //                     RecordWorldTransforms();
        //                 }
        //                 break;
        //             }
        //         case STATE_HELD:
        //             {
        //                 Vector3 parentPos = Vector3.zero;
        //                 Quaternion parentRot = Quaternion.identity;
        //                 if (localTransformFlag)
        //                 {
        //                     if (leftHandFlag)
        //                     {
        //                         parentPos = Owner.GetBonePosition(HumanBodyBones.LeftHand);
        //                         parentRot = Owner.GetBoneRotation(HumanBodyBones.LeftHand);
        //                     }
        //                     else
        //                     {
        //                         parentPos = Owner.GetBonePosition(HumanBodyBones.RightHand);
        //                         parentRot = Owner.GetBoneRotation(HumanBodyBones.RightHand);
        //                     }
        //                 }
        //
        //                 if (!localTransformFlag || parentPos == Vector3.zero)
        //                 {
        //                     parentPos = Owner.GetPosition();
        //                     parentRot = Owner.GetRotation();
        //                 }
        //                 RecordRelativeTransforms(parentPos, parentRot);
        //                 break;
        //             }
        //         case STATE_LOCAL_TO_OWNER:
        //             {
        //                 Vector3 parentPos = Owner.GetPosition();
        //                 Quaternion parentRot = Owner.GetRotation();
        //                 RecordRelativeTransforms(parentPos, parentRot);
        //                 break;
        //             }
        //         default:
        //             {
        //                 Vector3 parentPos;
        //                 Quaternion parentRot;
        //                 if (localTransformFlag && state <= STATE_BONE && state > STATE_BONE - ((sbyte)HumanBodyBones.LastBone))
        //                 {
        //                     HumanBodyBones parentBone = (HumanBodyBones)(STATE_BONE - state);
        //                     parentPos = Owner.GetBonePosition(parentBone);
        //                     parentRot = Owner.GetBoneRotation(parentBone);
        //                 }
        //                 else
        //                 {
        //                     parentPos = Owner.GetPosition();
        //                     parentRot = Owner.GetRotation();
        //                 }
        //                 RecordRelativeTransforms(parentPos, parentRot);
        //                 break;
        //             }
        //     }
        // }

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
                rigid.Sleep();
                SendCustomEventDelayedFrames(nameof(EnsureSleep), 0);
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
                    ApplyTransforms(pos, rot);
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
            return transform.localPosition != pos || Quaternion.Angle(transform.localRotation, rot) < 0.1f;
        }

        public bool WorldTransformDrifted()
        {
            return rigid.position != pos || Quaternion.Angle(rigid.rotation, rot) < 0.1f;
        }

        public bool RelativeTransformDrifted(Vector3 parentPos, Quaternion parentRot)
        {
            var invParentRot = Quaternion.Inverse(parentRot);
            var targetPos = invParentRot * (rigid.position - parentPos);
            var targetRot = invParentRot * rigid.rotation;
            return Vector3.Distance(targetPos, pos) > positionDesyncThreshold || Quaternion.Angle(targetRot, rot) > rotationDesyncThreshold;
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
        public bool _showInternalObjects = false;

        [HideInInspector]
        public bool showInternalObjects = false;

        [HideInInspector]
        public bool unparentInternalObjects = false;
#if UNITY_EDITOR && !COMPILER_UDONSHARP


        public void Reset()
        {
            AutoSetup();
            respawnHeight = VRC_SceneDescriptor.Instance.RespawnHeightY;
        }

        public void OnValidate()
        {
            // AutoSetupAsync();
            RefreshHideFlags();
        }

        public void RefreshHideFlags()
        {
            if (_showInternalObjects != showInternalObjects)
            {
                _showInternalObjects = showInternalObjects;
                if (!data || !looper)
                {
                    AutoSetup();
                }
                else
                {
                    data.RefreshHideFlags();
                    looper.RefreshHideFlags();
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
        }

        public void ForceSetup()
        {
            DestroyInternalObjectsAsync();
            EditorUtility.SetDirty(this);
            AutoSetup();
        }

        public void AutoSetup()
        {
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
            RefreshHideFlags();

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
        }

        public void SetupStates()
        {
            customStates = GetComponents<LightSyncState>();
            if (customStates.Length >= sbyte.MaxValue)
            {
                Debug.LogError("WHAT THE FUCK are you doing? How is it possible that you've got this many states on one LightSync?");
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
                if (!PrefabUtility.IsPartOfAnyPrefab(this))
                {
                    if (unparentInternalObjects && data.transform.parent != null)
                    {
                        data.transform.SetParent(null, false);
                    }
                    else if (data.transform.parent != gameObject)
                    {
                        data.transform.SetParent(transform, false);
                    }
                }
                data.transform.localPosition = Vector3.zero;
                data.transform.localRotation = Quaternion.identity;
                data.transform.localScale = Vector3.one;

                if (data.sync != this)
                {
                    data.sync = this;
                }

                data.RefreshHideFlags();

                if (networkDataOptimization == NetworkDataOptimization.Ultra && data is LightSyncDataUltra)
                {
                    return;
                }
                else if (networkDataOptimization == NetworkDataOptimization.High && data is LightSyncDataHigh)
                {
                    return;
                }
                else if (networkDataOptimization == NetworkDataOptimization.Low && data is LightSyncDataLow)
                {
                    return;
                }
                else if (networkDataOptimization == NetworkDataOptimization.Unoptimized && data is LightSyncDataUnoptimized)
                {
                    return;
                }

                DestroyImmediate(data.gameObject);
            }
            GameObject dataObject;
            switch (networkDataOptimization)
            {
                case NetworkDataOptimization.Ultra:
                    {
                        dataObject = new(name + "_dataUltra");
                        data = UdonSharpComponentExtensions.AddUdonSharpComponent<LightSyncDataUltra>(dataObject);
                        break;
                    }
                case NetworkDataOptimization.High:
                    {
                        dataObject = new(name + "_dataHigh");
                        data = UdonSharpComponentExtensions.AddUdonSharpComponent<LightSyncDataHigh>(dataObject);
                        break;
                    }
                case NetworkDataOptimization.Low:
                    {
                        dataObject = new(name + "_dataLow");
                        data = UdonSharpComponentExtensions.AddUdonSharpComponent<LightSyncDataLow>(dataObject);
                        break;
                    }
                case NetworkDataOptimization.Unoptimized:
                    {
                        dataObject = new(name + "_dataUnoptimized");
                        data = UdonSharpComponentExtensions.AddUdonSharpComponent<LightSyncDataUnoptimized>(dataObject);
                        break;
                    }
                default:
                    {
                        dataObject = new(name + "_dataDisabled ");
                        data = UdonSharpComponentExtensions.AddUdonSharpComponent<LightSyncDataDisabled>(dataObject);
                        break;
                    }
            }
            if (unparentInternalObjects)
            {
                dataObject.transform.SetParent(null, false);
            }
            else
            {
                dataObject.transform.SetParent(transform, false);
            }
            data.sync = this;
            data.RefreshHideFlags();
        }

        public void CreateLooperObject()
        {
            GameObject looperObject;
            if (looper != null)
            {
                looperObject = looper.gameObject;
                if (!PrefabUtility.IsPartOfAnyPrefab(this))
                {
                    if (unparentInternalObjects && looper.transform.parent != null)
                    {
                        looper.transform.SetParent(null, false);
                    }
                    else if (looper.transform.parent != gameObject)
                    {
                        looper.transform.SetParent(transform, false);
                    }
                }
                looper.transform.localPosition = Vector3.zero;
                looper.transform.localRotation = Quaternion.identity;
                looper.transform.localScale = Vector3.one;

                if (looper.sync != this)
                {
                    looper.sync = this;
                }

                if (looper.data != data)
                {
                    looper.data = data;
                }

                looper.RefreshHideFlags();

                looper.StopLoop();
            }
            else
            {
                looperObject = new(name + "_looper");
                if (unparentInternalObjects)
                {
                    looperObject.transform.SetParent(null, false);
                }
                else
                {
                    looperObject.transform.SetParent(transform, false);
                }
                looper = UdonSharpComponentExtensions.AddUdonSharpComponent<LightSyncLooperUpdate>(looperObject);
                looper.sync = this;
                looper.data = data;
                looper.RefreshHideFlags();
                looper.StopLoop();
            }

            if (fixedLooper != null)
            {
                if (fixedLooper.sync != this)
                {
                    fixedLooper.sync = this;
                }

                if (fixedLooper.data != data)
                {
                    fixedLooper.data = data;
                }
                fixedLooper.StopLoop();
            }
            else
            {
                fixedLooper = looper.GetComponent<LightSyncLooperFixedUpdate>();
                if (fixedLooper == null)
                {
                    fixedLooper = UdonSharpComponentExtensions.AddUdonSharpComponent<LightSyncLooperFixedUpdate>(looperObject);
                }
                fixedLooper.sync = this;
                fixedLooper.data = data;
                fixedLooper.StopLoop();
            }

            if (lateLooper != null)
            {
                if (lateLooper.sync != this)
                {
                    lateLooper.sync = this;
                }

                if (lateLooper.data != data)
                {
                    lateLooper.data = data;
                }
                lateLooper.StopLoop();
            }
            else
            {
                lateLooper = looper.GetComponent<LightSyncLooperPostLateUpdate>();
                if (lateLooper == null)
                {
                    lateLooper = UdonSharpComponentExtensions.AddUdonSharpComponent<LightSyncLooperPostLateUpdate>(looperObject);
                }
                lateLooper.sync = this;
                lateLooper.data = data;
                lateLooper.StopLoop();
            }
        }

        public void OnDestroy()
        {
            DestroyInternalObjectsAsync();
        }

        public void DestroyInternalObjectsAsync()
        {
            if (data)
            {
                data.DestroyAsync();
                data = null;
            }
            if (looper)
            {
                looper.DestroyAsync();
                looper = null;
                fixedLooper = null;
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
