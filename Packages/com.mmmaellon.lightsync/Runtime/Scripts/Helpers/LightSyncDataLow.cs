
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    public class LightSyncDataLow : LightSyncData
    {
        [NonSerialized, UdonSynced(UdonSyncMode.None)]
        int _data_state_flags;
        [NonSerialized, UdonSynced(UdonSyncMode.None)]
        Vector3 _pos = Vector3.zero;
        [NonSerialized, UdonSynced(UdonSyncMode.None)]
        Quaternion _rot = Quaternion.identity;
        [NonSerialized, UdonSynced(UdonSyncMode.None)]
        Vector3 _vel = Vector3.zero;
        [NonSerialized, UdonSynced(UdonSyncMode.None)]
        Vector3 _spin = Vector3.zero;

        int prevData;
        Vector3 prevPos = Vector3.zero;
        Quaternion prevRot = Quaternion.identity;
        Vector3 prevVel = Vector3.zero;
        Vector3 prevSpin = Vector3.zero;
        public override void RejectNewSyncData()
        {
            _pos = prevPos;
            _rot = prevRot;
            _vel = prevVel;
            _spin = prevSpin;
            _data_state_flags = prevData;
        }

        public override void AcceptNewSyncData()
        {
            pos = _pos;
            rot = _rot;
            vel = _vel;
            spin = _spin;
            state = (sbyte)(_data_state_flags >> 24);
            syncCount = (byte)((_data_state_flags >> 16) & 0xF);
            teleportCount = (byte)((_data_state_flags >> 8) & 0xF);
            localTransformFlag = (_data_state_flags & 0b10000000) != 0;
            kinematicFlag = (_data_state_flags & 0b01000000) != 0;
            pickupableFlag = (_data_state_flags & 0b00100000) != 0;
            leftHandFlag = (_data_state_flags & 0b00010000) != 0;
            bounceFlag = (_data_state_flags & 0b00001000) != 0;
            sleepFlag = (_data_state_flags & 0b00000100) != 0;
            loopTimingFlag = _data_state_flags & 0b00000011;

            prevPos = _pos;
            prevRot = _rot;
            prevVel = _vel;
            prevSpin = _spin;
            prevData = _data_state_flags;
        }


        public override void SyncNewData()
        {
            IncrementSyncCounter();
            _data_state_flags = (state << 24) | (syncCount << 16) | (teleportCount << 8) | loopTimingFlag;
            _data_state_flags |= localTransformFlag ? 0b10000000 : 0b0;
            _data_state_flags |= kinematicFlag ? 0b01000000 : 0b0;
            _data_state_flags |= pickupableFlag ? 0b00100000 : 0b0;
            _data_state_flags |= leftHandFlag ? 0b00010000 : 0b0;
            _data_state_flags |= bounceFlag ? 0b00001000 : 0b0;
            _data_state_flags |= sleepFlag ? 0b00000100 : 0b0;
            _pos = pos;
            _rot = rot;
            _vel = vel;
            _spin = spin;
        }

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

    }
}
