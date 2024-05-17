
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
        [UdonSynced(UdonSyncMode.None)]
        public byte _state;
        [UdonSynced(UdonSyncMode.None)]
        byte _syncCounter;//counts up every time we teleport
        byte localSyncCounter; //counts up every time we receive new data
        [UdonSynced(UdonSyncMode.None)]
        byte _teleportCount;//counts up every time we teleport
        byte localTeleportCount;
        bool teleportFlag
        {
            get => localTeleportCount != _teleportCount;
            set
            {
                if (value)
                {
                    if (IsOwner())
                    {
                        _teleportCount++;
                    }
                }
                else
                {
                    if (IsOwner())
                    {
                        _teleportCount = localTeleportCount;
                    }
                    localTeleportCount = _teleportCount;
                }
            }
        }
        byte flags;
        short state
        {
            get => _state;
            set
            {
                _state = (byte)value;
                if (IsOwner())
                {
                    RequestSerialization();
                }
            }
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
        short _rot_x;//rotation represented as an axis where where the magnitude is the amount of rotation
        [UdonSynced(UdonSyncMode.None)]
        short _rot_y;//rotation represented as an axis where where the magnitude is the amount of rotation
        [UdonSynced(UdonSyncMode.None)]
        short _rot_z;//rotation represented as an axis where where the magnitude is the amount of rotation
        [UdonSynced(UdonSyncMode.None)]
        short _spin_x;//rotation represented as an axis where where the magnitude is the amount of rotation
        [UdonSynced(UdonSyncMode.None)]
        short _spin_y;//rotation represented as an axis where where the magnitude is the amount of rotation
        [UdonSynced(UdonSyncMode.None)]
        short _spin_z;//rotation represented as an axis where where the magnitude is the amount of rotation

        const float quatMul = 90f;
        public Quaternion short3ToQuaternion(short x, short y, short z)
        {
            Vector3 axis = new Vector3(x, y, z);
            axis /= quatMul;
            return Quaternion.AngleAxis(axis.magnitude, axis.normalized);
        }
        public void quaternionToShort3(Quaternion q, out short x, out short y, out short z)
        {
            float angle;
            Vector3 axis;
            q.ToAngleAxis(out angle, out axis);
            axis *= angle * quatMul;
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
                quaternionToShort3(value, out _rot_x, out _rot_y, out _rot_z);
            }
        }
        Quaternion _spin;
        public Quaternion spin
        {
            get => _spin;
            set
            {
                _spin = value;
                quaternionToShort3(value, out _spin_x, out _spin_y, out _spin_z);
            }
        }

        public override void OnPreSerialization()
        {
            _syncCounter++;
            localSyncCounter = _syncCounter;

            sync.OnSendData();
        }

        public override void OnDeserialization(VRC.Udon.Common.DeserializationResult result)
        {
            _rot = short3ToQuaternion(_rot_x, _rot_y, _rot_z);
            _spin = short3ToQuaternion(_spin_x, _spin_y, _spin_z);

            sync.OnReceiveData(result);

            localSyncCounter = _syncCounter;
        }

        /*
            OWNERSHIP

            Just some utility functions
        */
        public VRCPlayerApi Owner;
        public void OnEnable()
        {
            Owner = Networking.GetOwner(gameObject);
        }
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            Owner = player;
        }
        //These are all the same, I'm just indecisive
        public bool IsOwner()
        {
            return Utilities.IsValid(Owner) && Owner.isLocal;
        }
        public bool IsOwnerLocal()
        {
            return Utilities.IsValid(Owner) && Owner.isLocal;
        }
        public bool IsLocalOwner()
        {
            return Utilities.IsValid(Owner) && Owner.isLocal;
        }
    }
}
