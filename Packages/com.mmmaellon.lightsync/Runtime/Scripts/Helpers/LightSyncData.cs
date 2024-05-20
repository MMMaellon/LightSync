
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class LightSyncData : UdonSharpBehaviour
    {
        public LightSync sync;
        [System.NonSerialized, UdonSynced(UdonSyncMode.None), FieldChangeCallback(nameof(state))]
        sbyte _state = STATE_SPAWN;
        public sbyte state
        {
            get => _state;
            set
            {
                prevState = _state;
                sync.OnExitState();
                _state = value;
                sync.OnEnterState();
                if (IsOwner())
                {
                    repeatStateCount++;
                    localRepeatStateCount = repeatStateCount;
                }
                foreach (LightSyncListener listener in sync.eventListeners)
                {
                    listener.OnChangeState(sync, prevState, value);
                }
            }
        }
        [System.NonSerialized, UdonSynced(UdonSyncMode.None)]
        byte syncCounter;//counts up every time we send new data
        byte localSyncCounter; //counts up every time we receive new data
        [System.NonSerialized, UdonSynced(UdonSyncMode.None), FieldChangeCallback(nameof(otherCounters))]
        byte _otherCounters = 1;//counts up every time we teleport. start with a teleport
        byte otherCounters
        {
            get => _otherCounters;
            set
            {
                _otherCounters = value;
                if (!IsOwner() && localRepeatStateCount != repeatStateCount)
                {
                    localRepeatStateCount = repeatStateCount;
                    state = state;
                }
            }
        }
        byte localTeleportCount = 0;
        byte teleportCount
        {
            get => (byte)(otherCounters & 0x00FF);
            set
            {
                if (value == 0)
                {
                    //make sure players always start the game with a teleport
                    value = 1;
                }
                otherCounters = (byte)((otherCounters & 0xFF00) | (value & 0x00FF));
            }
        }
        byte repeatStateCount
        {
            get => (byte)(otherCounters >> 4);
            set
            {
                otherCounters = (byte)((otherCounters & 0x00FF) | (value << 4));
            }
        }
        byte localRepeatStateCount;
        public bool teleportDirty
        {
            get => localTeleportCount != teleportCount;
            set
            {
                if (value)
                {
                    if (IsOwner())
                    {
                        teleportCount++;
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
        public bool newEvent
        {
            get => localTeleportCount != teleportCount;
        }
        [System.NonSerialized, UdonSynced(UdonSyncMode.None)]
        public byte flags;

        /*
            STATES
            Tells us what is going on with our object. Typically triggered by collider or pickup events.
            Negative state IDs are the built-in states.
            Positive or 0 state IDs are custom states
            Default state is -1, Teleport.
        */
        public const sbyte STATE_SPAWN = -1;//we instantly place objects and force them to be kinematic. Good for spawning things in.
        public const sbyte STATE_PHYSICS = -2;//we lerp objects into place and then let physics take over on the next physics frame
        public const sbyte STATE_SLEEP = -3;//we lerp objects into place and then force the object to sleep
        public const sbyte STATE_LEFT_HAND = -4;//we continually try to place an object in a player's left hand
        public const sbyte STATE_RIGHT_HAND = -5;//we continually try to place an object in a player's right hand
        public const sbyte STATE_NO_HAND = -6;//we teleport to a player's local position. used when avatar has no hands
        public const sbyte STATE_LOCAL_TO_OWNER = -6;//we continually try to place an object local to a player's position and rotation
        public const sbyte STATE_BONE = -7;//everything after this means the object is attached to an avatar's bones.
        /*
            SYNCED DATA

            Position and velocity are represented with Vector3's like normal
            Rotation and Spin (aka Angular Velocity) are represented with 3 shorts for network efficiency
                - When you try to read the values, it gets converted to Quaternions and Vector3's like normal
                - When you combine the 3 shorts together to make a Vector3, that Vector3 represents the axis of rotation. The magnitude of the Vector is how much it rotated.

        */
        [UdonSynced(UdonSyncMode.None)]
        public Vector3 pos;//position
        [UdonSynced(UdonSyncMode.None)]
        public Vector3 vel;//velocity
        [UdonSynced(UdonSyncMode.None)]
        public short _rot_x;//rotation represented as an axis where where the magnitude is the amount of rotation
        [UdonSynced(UdonSyncMode.None)]
        public short _rot_y;//rotation represented as an axis where where the magnitude is the amount of rotation
        [UdonSynced(UdonSyncMode.None)]
        public short _rot_z;//rotation represented as an axis where where the magnitude is the amount of rotation
        [UdonSynced(UdonSyncMode.None)]
        public short _spin_x;//rotation represented as an axis where where the magnitude is the amount of rotation
        [UdonSynced(UdonSyncMode.None)]
        public short _spin_y;//rotation represented as an axis where where the magnitude is the amount of rotation
        [UdonSynced(UdonSyncMode.None)]
        public short _spin_z;//rotation represented as an axis where where the magnitude is the amount of rotation

        const float shortMul = 90f;
        public static Vector3 Short3ToVector3(short x, short y, short z)
        {
            Vector3 v = new Vector3(x, y, z);
            v /= shortMul;
            return v;
        }
        public static void Vector3toShort3(Vector3 v, out short x, out short y, out short z)
        {
            v *= shortMul;
            x = (short)v.x;
            y = (short)v.y;
            z = (short)v.z;
        }
        public static Quaternion Short3ToQuaternion(short x, short y, short z)
        {
            Vector3 axis = new Vector3(x, y, z);
            axis /= shortMul;
            return Quaternion.AngleAxis(axis.magnitude, axis.normalized);
        }
        public static void QuaternionToShort3(Quaternion q, out short x, out short y, out short z)
        {
            q.ToAngleAxis(out float angle, out Vector3 axis);
            axis *= angle * shortMul;
            x = (short)axis.x;
            y = (short)axis.y;
            z = (short)axis.z;
        }
        Quaternion _rot;
        public Quaternion rot
        {
            get => _rot;
            set
            {
                _rot = value;
                QuaternionToShort3(value, out _rot_x, out _rot_y, out _rot_z);
            }
        }
        Vector3 _spin;
        public Vector3 spin
        {
            get => _spin;
            set
            {
                _spin = value;
                Vector3toShort3(value, out _spin_x, out _spin_y, out _spin_z);
            }
        }

        public override void OnPreSerialization()
        {
            if (syncCounter == byte.MaxValue)
            {
                syncCounter = 0;
            }
            else
            {
                syncCounter++;
            }
            localSyncCounter = syncCounter;
            sync.OnSendingData();
        }

        public sbyte prevState;
        byte prevTeleportCount;
        byte prevFlags;
        Vector3 prevPos;
        Vector3 prevVel;
        short prev_rot_x;
        short prev_rot_y;
        short prev_rot_z;
        short prev_spin_x;
        short prev_spin_y;
        short prev_spin_z;
        public override void OnDeserialization(VRC.Udon.Common.DeserializationResult result)
        {
            if (localSyncCounter > syncCounter && localSyncCounter - syncCounter > 8)//means we got updates out of order
            {
                //revert all synced values
                sync._print("Out of order network packet recieved");
                state = prevState;
                teleportCount = prevTeleportCount;
                flags = prevFlags;
                syncCounter = localSyncCounter;
                pos = prevPos;
                vel = prevVel;
                _rot_x = prev_rot_x;
                _rot_y = prev_rot_y;
                _rot_z = prev_rot_z;
                _spin_x = prev_spin_x;
                _spin_y = prev_spin_y;
                _spin_z = prev_spin_z;
                return;
            }
            _rot = Short3ToQuaternion(_rot_x, _rot_y, _rot_z);
            _spin = Short3ToVector3(_spin_x, _spin_y, _spin_z);

            sync.OnLerpStart();

            localSyncCounter = syncCounter;
            prevTeleportCount = teleportCount;
            prevFlags = flags;
            prevPos = pos;
            prevVel = vel;
            prev_rot_x = _rot_x;
            prev_rot_y = _rot_y;
            prev_rot_z = _rot_z;
            prev_spin_x = _spin_x;
            prev_spin_y = _spin_y;
            prev_spin_z = _spin_z;
        }

        public VRCPlayerApi Owner;
        VRCPlayerApi prevOwner;
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            prevOwner = Owner;
            Owner = player;
            foreach (LightSyncListener listener in sync.eventListeners)
            {
                listener.OnChangeOwner(sync, prevOwner, Owner);
            }
        }
        //These are all the same, I'm just indecisive
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
                }
            }

            gameObject.hideFlags = HideFlags.None;
        }
#endif
    }
}
