using System.Collections.Generic;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;


namespace MMMaellon.LightSync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public abstract class LightSyncData : UdonSharpBehaviour
    {
        public LightSync sync;
        public sbyte prevState;
        protected sbyte _state;
        public sbyte state
        {
            get => _state;
            set
            {
                prevState = _state;
                sync.OnExitState();
                _state = value;
                sync.OnEnterState();
                foreach (LightSyncListener listener in sync.eventListeners)
                {
                    listener.OnChangeState(sync, prevState, _state);
                }

            }
        }

        public Vector3 pos;
        public Quaternion rot;
        public Vector3 vel;
        public Vector3 spin;

        protected byte syncCount;
        protected byte localSyncCount; //counts up every time we receive new data
        protected byte teleportCount;
        protected byte localTeleportCount; //counts up every time we receive new data

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

        public bool localTransformFlag;
        public bool leftHandFlag;
        public bool kinematicFlag;
        public bool pickupableFlag;
        public bool bounceFlag;
        public bool sleepFlag;

        public float autoSmoothingTime
        {
#if !UNITY_EDITOR
            get => sync.smoothingTime > 0 ? sync.smoothingTime : Time.realtimeSinceStartup - Networking.SimulationTime(gameObject);
#else
            get => 0.25f;
#endif
        }

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
            return StateToStr(state) + " local:" + localTransformFlag + " left:" + leftHandFlag + " k:" + kinematicFlag + " p:" + pickupableFlag + " b:" + bounceFlag + " s:" + sleepFlag;
        }


        public void IncrementSyncCounter()
        {
            if (syncCount == byte.MaxValue)
            {
                syncCount = 0;
            }
            else
            {
                syncCount++;
            }
            localSyncCount = syncCount;
        }

        public override void OnDeserialization(VRC.Udon.Common.DeserializationResult result)
        {
            sync.Start();//set spawn and stuff if it's not already set
            if (localSyncCount > syncCount && localSyncCount - syncCount < 8)//means we got updates out of order
            {
                //revert all synced values
                sync._print("Out of order network packet received");
                RejectNewSyncData();
                return;
            }
            sync._print("NEW DATA: " + prettyPrint());
            AcceptNewSyncData();
            localSyncCount = syncCount;

            sync.looper.StartLoop();
        }

        public abstract void RejectNewSyncData();

        public abstract void AcceptNewSyncData();

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
                    foreach (LightSyncListener listener in sync.eventListeners)
                    {
                        if (Utilities.IsValid(listener))
                        {
                            listener.OnChangeOwner(sync, _Owner, value);
                        }
                    }
                    _Owner = value;
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
        VRCPlayerApi prevOwner;
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
                    StartCoroutine(Destroy());
                }
            }

            gameObject.hideFlags = HideFlags.None;
        }

        public IEnumerator<WaitForSeconds> Destroy()
        {
            yield return new WaitForSeconds(0);
            DestroyImmediate(gameObject);
        }
#endif
    }
}
