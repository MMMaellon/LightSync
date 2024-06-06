using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace MMMaellon.LightSync
{
    [AddComponentMenu("LightSync/LightSync")]
    [RequireComponent(typeof(Rigidbody))]
    public class LightSync : UdonSharpBehaviour
    {

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        enum NetworkDataOptimization
        {
            Ultra,
            High,
            Low,
            Unoptimized,
            DisableNetworking,
        }

        [SerializeField]
        NetworkDataOptimization networkDataOptimization = NetworkDataOptimization.Ultra;
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
        public float respawnHeight = -1001f;
        [Tooltip("Controls how long it takes for the object to smoothly move into the synced position. Set to 0 for VRChat's algorithm. Set negative for my autosmoothing algorithm. The more negative the smoother.")]
        public float smoothingTime = -0.25f;
        public bool allowTheftFromSelf = true;
        public bool allowTheftWhenAttachedToPlayer = true;
        public bool kinematicWhileHeld = true;
        public bool syncIsKinematic = true;
        public bool syncPickupable = false;
        public bool sleepOnSpawn = true;
        [Tooltip("Costs performance, but is required if a custom script changes the transform of this object")]
        public bool runEveryFrameOnOwner = false;

        //Extensions
        [HideInInspector]
        public Component[] eventListeners = new Component[0];
        [SerializeField]
        private UdonBehaviour[] behaviourEventListeners = new UdonBehaviour[0];
        [SerializeField]
        private LightSyncListener[] classEventListeners = new LightSyncListener[0];

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
        public bool syncParticleCollisions = true;
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
        public void Start()
        {
            data.Owner = Networking.GetOwner(gameObject);
            if (sleepOnSpawn && data.syncCount == 0)
            {
                rigid.Sleep();
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
            if (!IsOwner())
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
            transform.localPosition = position;
            transform.localRotation = rotation;
            data.state = LightSyncData.STATE_PHYSICS;
            if (shouldSleep)
            {
                data.sleepFlag = true;
                rigid.Sleep();
            }
        }

        public void TeleportToWorldSpace(Vector3 position, Quaternion rotation, bool shouldSleep)
        {
            if (!IsOwner())
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
            transform.position = position;
            transform.rotation = rotation;
            data.state = LightSyncData.STATE_PHYSICS;
            if (shouldSleep)
            {
                data.sleepFlag = true;
                rigid.Sleep();
            }
        }

        public void Sleep()
        {
            rigid.Sleep();
        }

        public void _print(string message)
        {
            if (!debugLogs)
            {
                return;
            }
            Debug.LogFormat(this, "[LightSync] {0}: {1}", name, message);
        }

        public int state
        {
            get => data.state;
            set
            {
                if (value < sbyte.MinValue || value > sbyte.MaxValue)
                {
                    _print("ERROR: Tried to set invalid STATE ID: " + value);
                    return;
                }
                data.state = (sbyte)value;
            }
        }

        public bool IsOwner()
        {
#if !COMPILER_UDONSHARP && UNITY_EDITOR
            return true;
#endif
            return data.IsOwner();
        }

        public void Sync()
        {
            if (!IsOwner())
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
            StartLoop();
        }

        public void StartLoop()
        {
            switch (data.loopTimingFlag)
            {
                case 0://Update
                    {
                        fixedLooper.StopLoop();
                        lateLooper.StopLoop();
                        looper.StartLoop();
                        break;
                    }
                case 1://FixedUpdate
                    {
                        looper.StopLoop();
                        lateLooper.StopLoop();
                        fixedLooper.StartLoop();
                        break;
                    }
                default://PostLateUpdate
                    {
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
            if (lastCollision == Time.frameCount)
            {
                return;
            }
            //decide if we should take ownership or not
            if (IsOwner() && takeOwnershipOfOtherObjectsOnCollision && Utilities.IsValid(other) && Utilities.IsValid(other.collider))
            {
                LightSync otherSync = other.collider.GetComponent<LightSync>();
                if (otherSync && otherSync.state == LightSyncData.STATE_PHYSICS && !otherSync.IsOwner() && otherSync.allowOthersToTakeOwnershipOnCollision && (!otherSync.takeOwnershipOfOtherObjectsOnCollision || otherSync.rigid.velocity.sqrMagnitude < rigid.velocity.sqrMagnitude))
                {
                    Networking.SetOwner(Networking.LocalPlayer, otherSync.gameObject);
                }
            }
            OnCollision();
        }

        public void OnCollisionExit(Collision other)
        {
            if (lastCollision == Time.frameCount)
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
            if (data.IsOwner())
            {
                if (state == LightSyncData.STATE_PHYSICS)
                {
                    data.bounceFlag = true;
                    Sync();
                }
            }
            else if (sleepCount <= 0 && minimumSleepFrames > 0 && data.sleepFlag && state == LightSyncData.STATE_PHYSICS)
            {
                EnsureSleep();
            }
            lastCollision = Time.frameCount;
        }

        public override void OnPickup()
        {
            if (!IsOwner())
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
            state = LightSyncData.STATE_HELD;
            Sync();
        }

        public override void OnDrop()
        {
            if (IsOwner() && IsHeld && !pickup.IsHeld)
            {
                //was truly dropped. We didn't like switch hands or something.
                state = LightSyncData.STATE_PHYSICS;
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
            get => state == LightSyncData.STATE_HELD;
        }

        public bool IsAttachedToPlayer
        {
            get => state <= LightSyncData.STATE_LOCAL_TO_OWNER;
        }

        // public void OnEnable()
        // {
        //     Networking.SetOwner(Networking.GetOwner(gameObject), data.gameObject);
        // }

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
            foreach (LightSyncListener listener in classEventListeners)
            {
                listener.OnChangeOwner(this, data.prevOwner, data.Owner);
            }

            foreach (UdonBehaviour behaviour in behaviourEventListeners)
            {
                behaviour.SetProgramVariable<LightSync>(LightSyncListener.syncVariableName, this);
                behaviour.SetProgramVariable<VRCPlayerApi>(LightSyncListener.prevOwnerVariableName, data.prevOwner);
                behaviour.SetProgramVariable<VRCPlayerApi>(LightSyncListener.currentOwnerVariableName, data.Owner);
                behaviour.SendCustomEvent(LightSyncListener.changeOwnerEventName);
            }
        }

        public void ChangeState(sbyte newStateID)
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
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
                    case LightSyncData.STATE_PHYSICS:
                        {
                            data.kinematicFlag = rigid.isKinematic;
                            data.pickupableFlag = pickup && pickup.pickupable;
                            data.localTransformFlag = !useWorldSpaceTransforms;
                            data.sleepFlag = rigid.IsSleeping();
                            data.leftHandFlag = false;
                            data.bounceFlag = false;//determine if we need it later
                            data.loopTimingFlag = LightSyncData.LOOP_FIXEDUPDATE;
                            break;
                        }
                    case LightSyncData.STATE_HELD:
                        {
                            if (data.prevState != state)
                            {
                                data.kinematicFlag = rigid.isKinematic;
                                data.pickupableFlag = pickup && pickup.pickupable;
                                lastPickupable = pickup.pickupable;
                                pickup.pickupable = allowTheftFromSelf;
                                lastKinematic = rigid.isKinematic;
                                rigid.isKinematic = kinematicWhileHeld;
                            }
                            data.localTransformFlag = !useWorldSpaceTransformsWhenHeldOrAttachedToPlayer;
                            data.sleepFlag = rigid.IsSleeping();
                            data.leftHandFlag = pickup.currentHand == VRC_Pickup.PickupHand.Left;
                            data.bounceFlag = true;
                            data.loopTimingFlag = LightSyncData.LOOP_POSTLATEUPDATE;

                            if (data.leftHandFlag)
                            {
                                if (data.Owner.GetBonePosition(HumanBodyBones.LeftHand) == Vector3.zero)
                                {
                                    data.localTransformFlag = false;
                                }
                            }
                            else if (data.Owner.GetBonePosition(HumanBodyBones.RightHand) == Vector3.zero)
                            {
                                data.localTransformFlag = false;
                            }
                            break;
                        }
                    case LightSyncData.STATE_LOCAL_TO_OWNER:
                        {
                            data.localTransformFlag = !useWorldSpaceTransformsWhenHeldOrAttachedToPlayer;
                            data.kinematicFlag = rigid.isKinematic;
                            data.sleepFlag = rigid.IsSleeping();
                            data.pickupableFlag = pickup && pickup.pickupable;
                            data.leftHandFlag = false;
                            data.bounceFlag = true;
                            data.loopTimingFlag = LightSyncData.LOOP_POSTLATEUPDATE;
                            break;
                        }
                    default:
                        {
                            data.localTransformFlag = !useWorldSpaceTransformsWhenHeldOrAttachedToPlayer;
                            data.kinematicFlag = rigid.isKinematic;
                            data.sleepFlag = rigid.IsSleeping();
                            data.pickupableFlag = pickup && pickup.pickupable;
                            data.leftHandFlag = false;
                            data.bounceFlag = true;
                            data.loopTimingFlag = LightSyncData.LOOP_POSTLATEUPDATE;
                            break;
                        }
                }
                RecordTransforms();
            }
            else
            {
                switch (state)
                {
                    case LightSyncData.STATE_PHYSICS:
                        {
                            if (syncIsKinematic)
                            {
                                rigid.isKinematic = data.kinematicFlag;
                            }
                            if (syncPickupable && pickup)
                            {
                                pickup.pickupable = data.pickupableFlag;
                            }
                            break;
                        }
                    case LightSyncData.STATE_HELD:
                        {
                            if (syncIsKinematic)
                            {
                                rigid.isKinematic = data.kinematicFlag || kinematicWhileHeld;
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
                                    pickup.pickupable = data.pickupableFlag && !pickup.DisallowTheft;
                                }
                                else
                                {
                                    lastPickupable = pickup.pickupable;
                                    pickup.pickupable = !pickup.DisallowTheft;
                                }
                            }
                            break;
                        }
                    case LightSyncData.STATE_LOCAL_TO_OWNER:
                        {
                            if (syncIsKinematic)
                            {
                                rigid.isKinematic = data.kinematicFlag || kinematicWhileAttachedToPlayer;
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
                                    pickup.pickupable = data.pickupableFlag && allowTheftWhenAttachedToPlayer;
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
                                rigid.isKinematic = data.kinematicFlag || kinematicWhileAttachedToPlayer;
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
                                    pickup.pickupable = data.pickupableFlag && allowTheftWhenAttachedToPlayer;
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

                foreach (LightSyncListener listener in classEventListeners)
                {
                    listener.OnChangeState(this, data.prevState, data.state);
                }
                foreach (UdonBehaviour behaviour in behaviourEventListeners)
                {
                    behaviour.SetProgramVariable<LightSync>(LightSyncListener.syncVariableName, this);
                    behaviour.SetProgramVariable<int>(LightSyncListener.prevStateVariableName, data.prevState);
                    behaviour.SetProgramVariable<int>(LightSyncListener.prevStateVariableName, data.state);
                    behaviour.SendCustomEvent(LightSyncListener.changeStateEventName);
                }
            }
        }

        public void OnExitState()
        {
            if (state >= 0 && state < customStates.Length)
            {
                customStates[state].OnExitState();
                return;
            }
            if (!IsOwner() || state < LightSyncData.STATE_PHYSICS)
            {
                if (syncIsKinematic)
                {
                    rigid.isKinematic = data.kinematicFlag;
                }
                else
                {
                    rigid.isKinematic = lastKinematic;
                }
                if (pickup)
                {
                    if (syncPickupable)
                    {
                        pickup.pickupable = data.pickupableFlag;
                    }
                    else
                    {
                        pickup.pickupable = lastPickupable;
                    }
                }
            }
        }

        bool _continueBool;
        public bool OnLerp(float elapsedTime, float autoSmoothedLerp)
        {
            if (!Utilities.IsValid(data.Owner))
            {
                return true;
            }
            if (state >= 0 && state < customStates.Length)
            {
                _continueBool = customStates[state].OnLerp(elapsedTime, autoSmoothedLerp);
                return _continueBool;
            }
            switch (state)
            {
                case LightSyncData.STATE_PHYSICS:
                    {
                        _continueBool = PhysicsLerp(elapsedTime, autoSmoothedLerp);
                        break;
                    }
                case LightSyncData.STATE_HELD:
                    {
                        _continueBool = HeldLerp(elapsedTime, autoSmoothedLerp);
                        break;
                    }
                case LightSyncData.STATE_LOCAL_TO_OWNER:
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
            return _continueBool;
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
                if (data.localTransformFlag)
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
                if (syncIsKinematic && (data.kinematicFlag != rigid.isKinematic))
                {
                    data.kinematicFlag = rigid.isKinematic;
                    shouldSync = true;
                }
                else if (syncPickupable && pickup && (data.pickupableFlag != pickup.pickupable))
                {
                    data.pickupableFlag = pickup.pickupable;
                    shouldSync = true;
                }
                else if (data.sleepFlag != rigid.IsSleeping())
                {
                    data.sleepFlag = rigid.IsSleeping();
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
                if (data.bounceFlag)
                {
                    //don't smoothly lerp the velocity to simulate a bounce
                    // endVel = (data.pos - recordedPos) / 2f;
                    endVel = Vector3.zero;
                }
                else
                {
                    endVel = data.vel;
                }
                tempPos = HermiteInterpolatePosition(recordedPos, startVel, data.pos, endVel, autoSmoothedLerp, data.autoSmoothingTime);
                tempRot = Quaternion.Slerp(recordedRot, data.rot, autoSmoothedLerp);
                ApplyTransforms(tempPos, tempRot);
                if (autoSmoothedLerp >= 1.0f)
                {
                    ApplyVelocities();
                    return false;
                }
                return true;
            }
        }

        Vector3 parentPos;
        Quaternion parentRot;
        bool HeldLerp(float elapsedTime, float autoSmoothedLerp)
        {
            if (data.localTransformFlag)
            {
                if (data.leftHandFlag)
                {
                    parentPos = data.Owner.GetBonePosition(HumanBodyBones.LeftHand);
                    parentRot = data.Owner.GetBoneRotation(HumanBodyBones.LeftHand);
                }
                else
                {
                    parentPos = data.Owner.GetBonePosition(HumanBodyBones.RightHand);
                    parentRot = data.Owner.GetBoneRotation(HumanBodyBones.RightHand);
                }
            }
            else
            {
                parentPos = data.Owner.GetPosition();
                parentRot = data.Owner.GetRotation();
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
                tempPos = HermiteInterpolatePosition(recordedPos, Vector3.zero, data.pos, Vector3.zero, autoSmoothedLerp, data.autoSmoothingTime);
                tempRot = Quaternion.Slerp(recordedRot, data.rot, autoSmoothedLerp);
                ApplyRelativeTransforms(parentPos, parentRot, tempPos, tempRot);
            }
            return true;
        }

        bool LocalLerp(float elapsedTime, float autoSmoothedLerp)
        {
            Vector3 parentPos = data.Owner.GetPosition();
            Quaternion parentRot = data.Owner.GetRotation();
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
                ApplyRelativeTransforms(parentPos, parentRot, data.pos, data.rot);
            }
            else
            {
                tempPos = HermiteInterpolatePosition(recordedPos, Vector3.zero, data.pos, Vector3.zero, autoSmoothedLerp, data.autoSmoothingTime);
                tempRot = Quaternion.Slerp(recordedRot, data.rot, autoSmoothedLerp);
                ApplyRelativeTransforms(parentPos, parentRot, tempPos, tempRot);
            }
            return true;
        }

        bool BoneLerp(float elapsedTime, float autoSmoothedLerp)
        {
            Vector3 parentPos;
            Quaternion parentRot;
            if (data.localTransformFlag && state <= LightSyncData.STATE_BONE && state > LightSyncData.STATE_BONE - ((sbyte)HumanBodyBones.LastBone))
            {
                HumanBodyBones parentBone = (HumanBodyBones)(LightSyncData.STATE_BONE - state);
                parentPos = data.Owner.GetBonePosition(parentBone);
                parentRot = data.Owner.GetBoneRotation(parentBone);
            }
            else
            {
                parentPos = data.Owner.GetPosition();
                parentRot = data.Owner.GetRotation();
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
                ApplyRelativeTransforms(parentPos, parentRot, data.pos, data.rot);
            }
            else
            {
                tempPos = HermiteInterpolatePosition(recordedPos, Vector3.zero, data.pos, Vector3.zero, autoSmoothedLerp, data.autoSmoothingTime);
                tempRot = Quaternion.Slerp(recordedRot, data.rot, autoSmoothedLerp);
                ApplyRelativeTransforms(parentPos, parentRot, tempPos, tempRot);
            }
            return true;
        }

        //Helper functions
        Vector3 recordedPos;
        Quaternion recordedRot;
        Vector3 recordedVel;
        Vector3 recordedSpin;
        public void RecordTransforms()
        {
            switch (state)
            {
                case LightSyncData.STATE_PHYSICS:
                    {
                        if (data.localTransformFlag)
                        {
                            RecordLocalTransforms();
                        }
                        else
                        {
                            RecordWorldTransforms();
                        }
                        break;
                    }
                case LightSyncData.STATE_HELD:
                    {
                        Vector3 parentPos = Vector3.zero;
                        Quaternion parentRot = Quaternion.identity;
                        if (data.localTransformFlag)
                        {
                            if (data.leftHandFlag)
                            {
                                parentPos = data.Owner.GetBonePosition(HumanBodyBones.LeftHand);
                                parentRot = data.Owner.GetBoneRotation(HumanBodyBones.LeftHand);
                            }
                            else
                            {
                                parentPos = data.Owner.GetBonePosition(HumanBodyBones.RightHand);
                                parentRot = data.Owner.GetBoneRotation(HumanBodyBones.RightHand);
                            }
                        }

                        if (!data.localTransformFlag || parentPos == Vector3.zero)
                        {
                            parentPos = data.Owner.GetPosition();
                            parentRot = data.Owner.GetRotation();
                        }
                        RecordRelativeTransforms(parentPos, parentRot);
                        break;
                    }
                case LightSyncData.STATE_LOCAL_TO_OWNER:
                    {
                        Vector3 parentPos = data.Owner.GetPosition();
                        Quaternion parentRot = data.Owner.GetRotation();
                        RecordRelativeTransforms(parentPos, parentRot);
                        break;
                    }
                default:
                    {
                        Vector3 parentPos;
                        Quaternion parentRot;
                        if (data.localTransformFlag && state <= LightSyncData.STATE_BONE && state > LightSyncData.STATE_BONE - ((sbyte)HumanBodyBones.LastBone))
                        {
                            HumanBodyBones parentBone = (HumanBodyBones)(LightSyncData.STATE_BONE - state);
                            parentPos = data.Owner.GetBonePosition(parentBone);
                            parentRot = data.Owner.GetBoneRotation(parentBone);
                        }
                        else
                        {
                            parentPos = data.Owner.GetPosition();
                            parentRot = data.Owner.GetRotation();
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
                data.pos = recordedPos;
                data.rot = recordedRot;
                data.vel = recordedVel;
                data.spin = recordedSpin;
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
                data.pos = recordedPos;
                data.rot = recordedRot;
                data.vel = recordedVel;
                data.spin = recordedSpin;
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
                data.pos = recordedPos;
                data.rot = recordedRot;
                data.vel = recordedVel;
                data.spin = recordedSpin;
            }
        }

        public void ApplyTransforms(Vector3 targetPos, Quaternion targetRot)
        {
            if (data.localTransformFlag)
            {
                transform.localPosition = targetPos;
                transform.localRotation = targetRot;
            }
            else
            {
                transform.position = targetPos;
                transform.rotation = targetRot;
            }
            rigid.position = transform.position;
            rigid.rotation = transform.rotation;
        }

        public void ApplyRelativeTransforms(Vector3 parentPos, Quaternion parentRot, Vector3 targetPos, Quaternion targetRot)
        {
            transform.position = parentPos + (parentRot * targetPos);
            transform.rotation = parentRot * targetRot;
            rigid.position = transform.position;
            rigid.rotation = transform.rotation;
        }

        public void ApplyVelocities()
        {
            if (rigid.isKinematic)
            {
                return;
            }
            if (data.sleepFlag)
            {
                rigid.Sleep();
                SendCustomEventDelayedFrames(nameof(EnsureSleep), 0);
            }
            else if (data.localTransformFlag)
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
            if (data.sleepFlag && data.state == LightSyncData.STATE_PHYSICS)
            {
                if (!rigid.IsSleeping())
                {
                    ApplyTransforms(data.pos, data.rot);
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
            rigid.velocity = data.vel;
            rigid.angularVelocity = data.spin;
        }

        public void ApplyLocalVelocities()
        {
            rigid.velocity = transform.rotation * data.vel;
            rigid.angularVelocity = transform.rotation * data.spin;
        }

        float lastDriftCheck = -1001f;
        public bool TransformDrifted()
        {
            if (Time.timeSinceLevelLoad - Mathf.Max(looper.startTime, lastDriftCheck) > data.autoSmoothingTime)
            {
                return false;
            }
            lastDriftCheck = Time.timeSinceLevelLoad;
            if (data.localTransformFlag)
            {
                return LocalTransformDrifted();
            }
            return WorldTransformDrifted();
        }

        public bool LocalTransformDrifted()
        {
            return transform.localPosition != data.pos || Quaternion.Angle(transform.localRotation, data.rot) < 0.1f;
        }

        public bool WorldTransformDrifted()
        {
            return rigid.position != data.pos || Quaternion.Angle(rigid.rotation, data.rot) < 0.1f;
        }

        public bool RelativeTransformDrifted(Vector3 parentPos, Quaternion parentRot)
        {
            var invParentRot = Quaternion.Inverse(parentRot);
            var targetPos = invParentRot * (rigid.position - parentPos);
            var targetRot = invParentRot * rigid.rotation;
            return Vector3.Distance(targetPos, data.pos) > positionDesyncThreshold || Quaternion.Angle(targetRot, data.rot) > rotationDesyncThreshold;
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
        [SerializeField]
        bool _showInternalObjects = false;

        [HideInInspector]
        public bool showInternalObjects = false;

        [SerializeField]
        bool _detachInternalObjects = false;

        [HideInInspector]
        public bool unparentInternalObjects = false;
#if UNITY_EDITOR && !COMPILER_UDONSHARP


        public void Reset()
        {
            AutoSetup();

            respawnHeight = VRC_SceneDescriptor.Instance.RespawnHeightY;

            rigid = GetComponent<Rigidbody>();
            pickup = GetComponent<VRC_Pickup>();
        }

        public void OnValidate()
        {
            AutoSetupAsync();
        }

        public void RefreshHideFlags()
        {
            if (_showInternalObjects != showInternalObjects)
            {
                _showInternalObjects = showInternalObjects;
                data.RefreshHideFlags();
                looper.RefreshHideFlags();
            }
        }

        public void AutoSetupAsync()
        {
            if (gameObject.activeInHierarchy && enabled)//prevents log spam in play mode
            {
                StartCoroutine(AutoSetup());
            }
        }

        public IEnumerator<WaitForSeconds> AutoSetup()
        {
            yield return new WaitForSeconds(0);
            _print("Auto setup");
            rigid = GetComponent<Rigidbody>();
            pickup = GetComponent<VRC_Pickup>();
            lastPickupable = pickup.pickupable;
            lastKinematic = rigid.isKinematic;
            CreateDataObject();
            CreateLooperObject();
            RefreshHideFlags();
            SetupStates();
            SetupEnhancements();
            SetupListeners();

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
            data.kinematicFlag = rigid.isKinematic;
            data.bounceFlag = false;
            data.pickupableFlag = pickup && pickup.pickupable;
            data.sleepFlag = sleepOnSpawn;
            if (enterFirstCustomStateOnStart && customStates.Length > 0)
            {
                customStates[0].EnterState();
            }
            else
            {
                data.state = LightSyncData.STATE_PHYSICS;
            }
            data.SyncNewData();
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
                customStates[i].AutoSetup();
            }
        }

        public void SetupEnhancements()
        {
            foreach (var enhancement in GetComponents<LightSyncEnhancement>())
            {
                enhancement.sync = this;
                enhancement.AutoSetup();
            }
        }

        public void SetupListeners()
        {
            eventListeners = eventListeners.Where(obj => Utilities.IsValid(obj)).ToArray();
            List<LightSyncListener> classListeners = new();
            List<UdonBehaviour> behaviourListeners = new();
            LightSyncListener classListener;
            UdonBehaviour behaviourListener;
            foreach (Component listener in eventListeners)
            {
                classListener = (LightSyncListener)listener;
                behaviourListener = (UdonBehaviour)listener;
                if (classListener)
                {
                    classListeners.Add(classListener);
                }
                else if (behaviourListener)
                {
                    behaviourListeners.Add(behaviourListener);
                }
            }
            classEventListeners = classListeners.ToArray();
            behaviourEventListeners = behaviourListeners.ToArray();
        }

        public void CreateDataObject()
        {
            if (data != null)
            {
                if (unparentInternalObjects && data.transform.parent != null)
                {
                    data.transform.SetParent(null, false);
                    data.transform.localPosition = Vector3.zero;
                    data.transform.localRotation = Quaternion.identity;
                    data.transform.localScale = Vector3.one;
                }
                else if (data.transform.parent != gameObject)
                {
                    data.transform.SetParent(transform, false);
                    data.transform.localPosition = Vector3.zero;
                    data.transform.localRotation = Quaternion.identity;
                    data.transform.localScale = Vector3.one;
                }

                if (data.sync != this)
                {
                    data.sync = this;
                }

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
                        dataObject.transform.SetParent(transform, false);
                        data = dataObject.AddComponent<LightSyncDataUltra>();
                        break;
                    }
                case NetworkDataOptimization.High:
                    {
                        dataObject = new(name + "_dataHigh");
                        dataObject.transform.SetParent(transform, false);
                        data = dataObject.AddComponent<LightSyncDataHigh>();
                        break;
                    }
                case NetworkDataOptimization.Low:
                    {
                        dataObject = new(name + "_dataLow");
                        dataObject.transform.SetParent(transform, false);
                        data = dataObject.AddComponent<LightSyncDataLow>();
                        break;
                    }
                case NetworkDataOptimization.Unoptimized:
                    {
                        dataObject = new(name + "_dataUnoptimized");
                        dataObject.transform.SetParent(transform, false);
                        data = dataObject.AddComponent<LightSyncDataUnoptimized>();
                        break;
                    }
                default:
                    {
                        dataObject = new(name + "_dataDisabled ");
                        data = dataObject.AddComponent<LightSyncDataDisabled>();
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
                if (unparentInternalObjects && looper.transform.parent != null)
                {
                    looper.transform.SetParent(null, false);
                    looper.transform.localPosition = Vector3.zero;
                    looper.transform.localRotation = Quaternion.identity;
                    looper.transform.localScale = Vector3.one;
                }
                else if (looper.transform.parent != gameObject)
                {
                    looper.transform.SetParent(transform, false);
                    looper.transform.localPosition = Vector3.zero;
                    looper.transform.localRotation = Quaternion.identity;
                    looper.transform.localScale = Vector3.one;
                }

                if (looper.sync != this)
                {
                    looper.sync = this;
                }

                if (looper.data != data)
                {
                    looper.data = data;
                }
                looper.StopLoop();
            }
            else
            {
                looperObject = new(name + "_looper");
                looperObject.transform.SetParent(transform, false);
                looper = looperObject.AddComponent<LightSyncLooperUpdate>();
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
                    fixedLooper = looperObject.AddComponent<LightSyncLooperFixedUpdate>();
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
                    lateLooper = looperObject.AddComponent<LightSyncLooperPostLateUpdate>();
                }
                lateLooper.sync = this;
                lateLooper.data = data;
                lateLooper.StopLoop();
            }
        }

        public void OnDestroy()
        {
            if (data)
            {
                data.DestroyAsync();
            }
            if (looper)
            {
                looper.DestroyAsync();
            }
        }
#endif
    }
}
