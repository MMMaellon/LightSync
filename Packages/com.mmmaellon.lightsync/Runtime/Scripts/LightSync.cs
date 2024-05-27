using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Components;
using VRC.Udon.Common;

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
        [Tooltip("Controls how long it takes for the object to smoothly move into the synced position. Set to negative for auto.")]
        public float smoothingTime = -1001f;
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

        [System.NonSerialized]
        public bool spawnSet = false;
        Vector3 spawnPos;
        Quaternion spawnRot;
        public void Start()
        {
            if (spawnSet)
            {
                return;
            }
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
            data.Owner = Networking.GetOwner(gameObject);
            spawnSet = true;
            if (IsOwner())
            {
                data.kinematicFlag = rigid.isKinematic;
                data.bounceFlag = false;
                data.pickupableFlag = pickup && pickup.pickupable;
                if (sleepOnSpawn)
                {
                    rigid.Sleep();
                }
                data.sleepFlag = sleepOnSpawn;
                if (enterFirstCustomStateOnStart && customStates.Length > 0)
                {
                    customStates[0].EnterState();
                }
                else
                {
                    data.state = LightSyncData.STATE_PHYSICS;
                    RecordTransforms();
                    Sync();
                }
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
            return data.IsOwner();
        }

        public void Sync()
        {
            if (!IsOwner())
            {
                Networking.SetOwner(Networking.LocalPlayer, data.gameObject);
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
        public void SyncIfOwner()
        {
            if (IsOwner())
            {
                StartLoop();
            }
        }
        [System.NonSerialized]
        public int lastCollisionEnter = -1001;
        public void OnCollisionEnter(Collision other)
        {
            if (lastCollisionEnter == Time.frameCount)
            {
                return;
            }
            lastCollisionEnter = Time.frameCount;
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
                if (state == LightSyncData.STATE_PHYSICS)
                {
                    Sync();
                }
            }
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
            state = newStateID;
            Sync();
        }

        public void OnEnterState()
        {
            if (state >= 0 && state < customStates.Length)
            {
                customStates[state].OnEnterState();
                return;
            }
            if (IsOwner())
            {
                data.kinematicFlag = rigid.isKinematic;
                if (pickup)
                {
                    if (state == LightSyncData.STATE_HELD)
                    {
                        data.leftHandFlag = pickup.currentHand == VRC_Pickup.PickupHand.Left;
                    }
                    else
                    {
                        data.pickupableFlag = pickup.pickupable;
                    }
                }
                data.sleepFlag = rigid.IsSleeping();
            }
            switch (state)
            {
                case LightSyncData.STATE_PHYSICS:
                    {
                        if (IsOwner())
                        {
                            data.localTransformFlag = !useWorldSpaceTransforms;
                            data.loopTimingFlag = LightSyncData.LOOP_FIXEDUPDATE;
                        }
                        else
                        {
                            if (syncIsKinematic)
                            {
                                rigid.isKinematic = data.kinematicFlag;
                            }
                            if (syncPickupable && pickup)
                            {
                                pickup.pickupable = data.pickupableFlag;
                            }
                        }
                        break;
                    }
                case LightSyncData.STATE_HELD:
                    {
                        if (syncIsKinematic)
                        {
                            rigid.isKinematic = data.kinematicFlag || kinematicWhileHeld;
                        }
                        if (syncPickupable && pickup)
                        {
                            if (IsOwner())
                            {
                                pickup.pickupable = data.pickupableFlag && allowTheftFromSelf;
                            }
                            else
                            {
                                pickup.pickupable = data.pickupableFlag && !pickup.DisallowTheft;
                            }
                        }
                        if (IsOwner())
                        {
                            data.localTransformFlag = !useWorldSpaceTransformsWhenHeldOrAttachedToPlayer;
                            data.loopTimingFlag = LightSyncData.LOOP_POSTLATEUPDATE;
                        }
                        break;
                    }
                case LightSyncData.STATE_LOCAL_TO_OWNER:
                    {
                        if (syncIsKinematic)
                        {
                            rigid.isKinematic = data.kinematicFlag || kinematicWhileAttachedToPlayer;
                        }
                        if (syncPickupable && pickup)
                        {
                            if (!IsOwner())
                            {
                                pickup.pickupable = data.pickupableFlag && allowTheftWhenAttachedToPlayer;
                            }
                        }
                        if (IsOwner())
                        {
                            data.loopTimingFlag = LightSyncData.LOOP_POSTLATEUPDATE;
                        }
                        break;
                    }

                default:
                    {
                        if (syncIsKinematic)
                        {
                            rigid.isKinematic = data.kinematicFlag || kinematicWhileAttachedToPlayer;
                        }
                        if (syncPickupable && pickup)
                        {
                            if (!IsOwner())
                            {
                                pickup.pickupable = data.pickupableFlag && allowTheftWhenAttachedToPlayer;
                            }
                        }
                        if (IsOwner())
                        {
                            data.loopTimingFlag = LightSyncData.LOOP_POSTLATEUPDATE;
                            data.localTransformFlag = !useWorldSpaceTransformsWhenHeldOrAttachedToPlayer;
                        }
                        break;
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
            if (syncIsKinematic)
            {
                rigid.isKinematic = data.kinematicFlag;
            }
            if (syncPickupable && pickup)
            {
                pickup.pickupable = data.pickupableFlag;
            }
        }

        bool tempContinueBool;
        public bool OnLerp(float elapsedTime, float autoSmoothedLerp)
        {
            if (!Utilities.IsValid(data.Owner))
            {
                return true;
            }
            if (state >= 0 && state < customStates.Length)
            {
                tempContinueBool = customStates[state].OnLerp(elapsedTime, autoSmoothedLerp);
                return tempContinueBool;
            }
            switch (state)
            {
                case LightSyncData.STATE_PHYSICS:
                    {
                        tempContinueBool = PhysicsLerp(elapsedTime, autoSmoothedLerp);
                        break;
                    }
                case LightSyncData.STATE_HELD:
                    {
                        tempContinueBool = HeldLerp(elapsedTime, autoSmoothedLerp);
                        break;
                    }
                case LightSyncData.STATE_LOCAL_TO_OWNER:
                    {
                        tempContinueBool = LocalLerp(elapsedTime, autoSmoothedLerp);
                        break;
                    }
                default:
                    {
                        tempContinueBool = BoneLerp(elapsedTime, autoSmoothedLerp);
                        break;
                    }
            }
            return tempContinueBool;
        }

        Vector3 tempPos;
        Quaternion tempRot;
        bool PhysicsLerp(float elapsedTime, float autoSmoothedLerp)
        {
            if (elapsedTime == 0)
            {
                if (useWorldSpaceTransforms)
                {
                    RecordWorldTransforms();
                }
                else
                {
                    RecordLocalTransforms();
                }
            }
            if (IsOwner())
            {
                if (runEveryFrameOnOwner)
                {
                    if (TransformDrifted())
                    {
                        Sync();
                    }
                }
                if (rigid.isKinematic)
                {
                    if (!data.kinematicFlag)
                    {
                        data.kinematicFlag = true;
                        Sync();
                    }
                    return runEveryFrameOnOwner;
                }
                else if (rigid.IsSleeping())
                {
                    if (!data.sleepFlag)
                    {
                        data.sleepFlag = true;
                        Sync();
                    }
                    return runEveryFrameOnOwner;
                }
                return true;
            }
            else
            {
                tempPos = HermiteInterpolatePosition(recordedPos, recordedVel, data.pos, data.vel, autoSmoothedLerp, data.autoSmoothingTime);
                tempRot = Quaternion.Slerp(recordedRot, data.rot, autoSmoothedLerp);
                ApplyTransforms(tempPos, tempRot);
                var remainingTime = data.autoSmoothingTime - elapsedTime;
                if (Mathf.Abs(remainingTime - Time.fixedDeltaTime) < Mathf.Abs(remainingTime - (2 * Time.fixedDeltaTime)))//the next physics frame is the closest to the end
                {
                    ApplyVelocities();
                    return false;
                }
                return true;
            }
        }

        bool HeldLerp(float elapsedTime, float autoSmoothedLerp)
        {
            Vector3 parentPos;
            Quaternion parentRot;
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
                        if (IsOwner())
                        {
                            data.localTransformFlag = !useWorldSpaceTransforms;
                        }
                        if (useWorldSpaceTransforms)
                        {
                            RecordWorldTransforms();
                        }
                        else
                        {
                            RecordLocalTransforms();
                        }
                        break;
                    }
                case LightSyncData.STATE_HELD:
                    {
                        Vector3 parentPos = Vector3.zero;
                        Quaternion parentRot = Quaternion.identity;
                        if (IsOwner())
                        {
                            data.localTransformFlag = true;
                            data.leftHandFlag = pickup.currentHand == VRC_Pickup.PickupHand.Left;
                        }
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
                            if (IsOwner())
                            {
                                data.localTransformFlag = false;
                            }
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
                        if (IsOwner())
                        {
                            data.localTransformFlag = true;
                        }
                        if (data.localTransformFlag && state <= LightSyncData.STATE_BONE && state > LightSyncData.STATE_BONE - ((sbyte)HumanBodyBones.LastBone))
                        {
                            HumanBodyBones parentBone = (HumanBodyBones)(LightSyncData.STATE_BONE - state);
                            parentPos = data.Owner.GetBonePosition(parentBone);
                            parentRot = data.Owner.GetBoneRotation(parentBone);
                        }
                        else
                        {
                            if (IsOwner())
                            {
                                data.localTransformFlag = false;
                            }
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
                data.RequestSerialization();
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
                data.RequestSerialization();
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
                data.RequestSerialization();
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
            }
            if (data.localTransformFlag)
            {
                ApplyLocalVelocities();
            }
            else
            {
                ApplyWorldVelocities();
            }
        }

        public void ApplyWorldVelocities()
        {
            rigid.velocity = data.vel;
            rigid.angularVelocity = data.spin;
        }

        public void ApplyLocalVelocities()
        {
            rigid.velocity = rigid.rotation * data.vel;
            rigid.angularVelocity = rigid.rotation * data.spin;
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

#if UNITY_EDITOR && !COMPILER_UDONSHARP

        bool _showInternalObjects = false;

        [HideInInspector]
        public bool showInternalObjects = false;

        public void Reset()
        {
            AutoSetup();

            respawnHeight = VRC_SceneDescriptor.Instance.RespawnHeightY;

            rigid = GetComponent<Rigidbody>();
            pickup = GetComponent<VRC_Pickup>();
        }

        public void OnValidate()
        {
            StartCoroutine(AutoSetup());
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

        public IEnumerator<WaitForSeconds> AutoSetup()
        {
            yield return new WaitForSeconds(0);
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
            dataObject.transform.SetParent(transform, false);
            data.sync = this;
            data.RefreshHideFlags();
        }

        public void CreateLooperObject()
        {
            GameObject looperObject;
            if (looper != null)
            {
                looperObject = looper.gameObject;
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
            Debug.LogWarning("OnDestroy");
            if (data)
            {
                data.StartCoroutine(data.Destroy());
            }
            if (looper)
            {
                looper.StartCoroutine(looper.Destroy());
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
}
#endif

