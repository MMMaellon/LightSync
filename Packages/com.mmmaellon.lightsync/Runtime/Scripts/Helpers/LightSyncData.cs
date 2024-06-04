using System.Collections.Generic;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;


namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]//prevents it from showing up in the add component menu
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public abstract class LightSyncData : UdonSharpBehaviour
    {
        public LightSync sync;
        public sbyte prevState;
        sbyte _state = STATE_PHYSICS;
        public sbyte state
        {
            get => _state;
            set
            {
                if (IsOwner())
                {
                    prevState = _state;
                    sync.OnExitState();
                }
                _state = value;
                if (IsOwner())
                {
                    sync.OnEnterState();
                }

            }
        }

        Vector3 _temp_pos;
        public Vector3 pos
        {
            get => _temp_pos;
            set
            {
                _temp_pos = value;
            }
        }
        public Quaternion rot;
        public Vector3 vel;
        public Vector3 spin;

        [System.NonSerialized]
        public byte syncCount;
        [System.NonSerialized]
        public byte localSyncCount; //counts up every time we receive new data
        protected byte teleportCount = 1;
        protected byte localTeleportCount; //counts up every time we receive new data;
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

        public bool localTransformFlag = true;
        public bool leftHandFlag;
        public bool kinematicFlag;
        public bool pickupableFlag = true;
        public bool bounceFlag;
        public bool sleepFlag = true;

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

        public override void OnPreSerialization()
        {
            SyncNewData();
            sync._print("SENDING DATA: " + prettyPrint());
        }

        float remainingSmoothingTime;
        float lastSmoothingCalcTime;
        public void CalcSmoothingTime(float sendTime)
        {
            if (sync.smoothingTime > 0)
            {
                autoSmoothingTime = sync.smoothingTime;
            }
            else if (sync.smoothingTime == 0)
            {
                autoSmoothingTime = Time.realtimeSinceStartup - Networking.SimulationTime(gameObject);
            }
            else
            {
                remainingSmoothingTime = -sync.smoothingTime - (Time.realtimeSinceStartup - sendTime);
                if (autoSmoothingTime == 0)
                {
                    autoSmoothingTime = Mathf.Clamp(remainingSmoothingTime, 0, Time.realtimeSinceStartup - lastSmoothingCalcTime);
                }
                else
                {
                    autoSmoothingTime = Mathf.Lerp(autoSmoothingTime, Mathf.Clamp(remainingSmoothingTime, 0, Time.realtimeSinceStartup - lastSmoothingCalcTime), Mathf.Abs((remainingSmoothingTime - autoSmoothingTime) / sync.smoothingTime));
                }
            }
            lastSmoothingCalcTime = Time.realtimeSinceStartup;
        }
        public override void OnDeserialization(VRC.Udon.Common.DeserializationResult result)
        {
            CalcSmoothingTime(result.sendTime);
            if (localSyncCount > syncCount && localSyncCount - syncCount < 8)//means we got updates out of order
            {
                //revert all synced values
                sync._print("Out of order network packet received");
                RejectNewSyncData();
                return;
            }
            sync.StopLoop();
            prevState = _state;
            sync.OnExitState();
            AcceptNewSyncData();
            localSyncCount = syncCount;
            sync.OnEnterState();

            sync._print("NEW DATA: " + prettyPrint());
            sync.StartLoop();
        }

        public abstract void RejectNewSyncData();

        public abstract void AcceptNewSyncData();

        public abstract void SyncNewData();

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
                    sync.OnChangeOwner();
                    if (IsOwner())
                    {
                        if (state <= STATE_HELD && (sync.pickup == null || !sync.pickup.IsHeld))
                        {
                            state = STATE_PHYSICS;
                        }
                    }
                    else
                    {
                        if (sync.pickup)
                        {
                            sync.pickup.Drop();
                        }
                    }
                }
            }
        }
        [System.NonSerialized]
        public VRCPlayerApi prevOwner;
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            Owner = player;
        }

        public bool IsOwner()
        {
            return Utilities.IsValid(Owner) && Owner.isLocal;
        }
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public void OnValidate()
        {
            RefreshHideFlags();
        }

        public void RefreshHideFlags()
        {
            if (sync != null)
            {
                if (sync.data == this)
                {
                    if (sync.showInternalObjects)
                    {
                        gameObject.hideFlags = HideFlags.None;
                    }
                    else
                    {
                        gameObject.hideFlags = HideFlags.HideInHierarchy;
                    }
                    return;
                }
                else
                {
                    sync = null;
                    DestroyAsync();
                }
            }

            gameObject.hideFlags = HideFlags.None;
        }
        public void DestroyAsync()
        {
            if (gameObject.activeInHierarchy && enabled)//prevents log spam in play mode
            {
                StartCoroutine(Destroy());
            }
        }
        public IEnumerator<WaitForSeconds> Destroy()
        {
            yield return new WaitForSeconds(0);
            DestroyImmediate(gameObject);
        }
#endif
    }
}
