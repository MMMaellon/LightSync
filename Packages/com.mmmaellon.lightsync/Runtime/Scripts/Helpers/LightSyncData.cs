﻿
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
        sbyte _state = STATE_PHYSICS;
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
            get => (byte)(otherCounters & 0b00001111);
            set
            {
                if (value == 0)
                {
                    //make sure players always start the game with a teleport
                    value = 1;
                }
                otherCounters = (byte)((otherCounters & 0b11110000) | (value & 0b00001111));
            }
        }
        byte repeatStateCount
        {
            get => (byte)(otherCounters >> 4);
            set
            {
                otherCounters = (byte)((otherCounters & 0b00001111) | ((value << 4) & 0xFF));
            }
        }
        byte localRepeatStateCount;
        public bool teleportFlag
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
            get => localRepeatStateCount != repeatStateCount;
        }
        [System.NonSerialized, UdonSynced(UdonSyncMode.None)]
        public byte flags;

        public bool localTransformFlag
        {
            get => (flags & 0b10000000) != 0;
            set
            {
                if (value)
                {
                    flags |= 0b10000000;
                }
                else
                {
                    flags &= 0b01111111;
                }
            }
        }

        public bool leftHandFlag
        {
            get => (flags & 0b01000000) != 0;
            set
            {
                if (value)
                {
                    flags |= 0b01000000;
                }
                else
                {
                    flags &= 0b10111111;
                }
            }
        }

        public bool kinematicFlag
        {
            get => (flags & 0b00100000) != 0;
            set
            {
                if (value)
                {
                    flags |= 0b00100000;
                }
                else
                {
                    flags &= 0b11011111;
                }
            }
        }

        public bool pickupableFlag
        {
            get => (flags & 0b00010000) != 0;
            set
            {
                if (value)
                {
                    flags |= 0b00010000;
                }
                else
                {
                    flags &= 0b11101111;
                }
            }
        }

        public bool bounceFlag
        {
            get => (flags & 0b00001000) != 0;
            set
            {
                if (value)
                {
                    flags |= 0b00001000;
                }
                else
                {
                    flags &= 0b11110111;
                }
            }
        }

        public bool sleepFlag
        {
            get => (flags & 0b00000100) != 0;
            set
            {
                if (value)
                {
                    flags |= 0b00000100;
                }
                else
                {
                    flags &= 0b11111011;
                }
            }
        }

        /*
            STATES
            Tells us what is going on with our object. Typically triggered by collider or pickup events.
            Negative state IDs are the built-in states.
            Positive or 0 state IDs are custom states
            Default state is -1, Teleport.
        */
        public const sbyte STATE_PHYSICS = -1;//we lerp objects into place and then let physics take over on the next physics frame
        public const sbyte STATE_HELD = -2;//we continually try to place an object in a player's left hand
        public const sbyte STATE_LOCAL_TO_OWNER = -3;//we continually try to place an object local to a player's position and rotation
        public const sbyte STATE_BONE = -4;//everything after this means the object is attached to an avatar's bones.
        public static string StateToStr(sbyte s)
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
        public string prettyPrint()
        {
            return StateToStr(state) + " local:" + localTransformFlag + " left:" + leftHandFlag + " k:" + kinematicFlag + " p:" + pickupableFlag + " b:" + bounceFlag + " s:" + sleepFlag;
        }

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
            if (localSyncCounter > syncCounter && localSyncCounter - syncCounter < 8)//means we got updates out of order
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

            sync._print("NEW DATA: " + prettyPrint());
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