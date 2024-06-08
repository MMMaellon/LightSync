
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]//prevents it from showing up in the add component menu
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class LightSyncDataLow : LightSyncData
    {
        [UdonSynced(UdonSyncMode.None)]
        int _data_state_flags;
        [UdonSynced(UdonSyncMode.None)]
        Vector3 _pos = Vector3.zero;
        [UdonSynced(UdonSyncMode.None)]
        Quaternion _rot = Quaternion.identity;
        [UdonSynced(UdonSyncMode.None)]
        Vector3 _vel = Vector3.zero;
        [UdonSynced(UdonSyncMode.None)]
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
            sync.pos = _pos;
            sync.rot = _rot;
            sync.vel = _vel;
            sync.spin = _spin;
            sync.state = (sbyte)(_data_state_flags >> 24);
            sync.syncCount = (byte)((_data_state_flags >> 16) & 0xF);
            sync.teleportCount = (byte)((_data_state_flags >> 8) & 0xF);
            sync.localTransformFlag = (_data_state_flags & 0b10000000) != 0;
            sync.kinematicFlag = (_data_state_flags & 0b01000000) != 0;
            sync.pickupableFlag = (_data_state_flags & 0b00100000) != 0;
            sync.leftHandFlag = (_data_state_flags & 0b00010000) != 0;
            sync.bounceFlag = (_data_state_flags & 0b00001000) != 0;
            sync.sleepFlag = (_data_state_flags & 0b00000100) != 0;
            sync.loopTimingFlag = _data_state_flags & 0b00000011;

            prevPos = _pos;
            prevRot = _rot;
            prevVel = _vel;
            prevSpin = _spin;
            prevData = _data_state_flags;
        }


        public override void SyncNewData()
        {
            sync.IncrementSyncCounter();
            _data_state_flags = (sync.state << 24) | (sync.syncCount << 16) | (sync.teleportCount << 8) | sync.loopTimingFlag;
            _data_state_flags |= sync.localTransformFlag ? 0b10000000 : 0b0;
            _data_state_flags |= sync.kinematicFlag ? 0b01000000 : 0b0;
            _data_state_flags |= sync.pickupableFlag ? 0b00100000 : 0b0;
            _data_state_flags |= sync.leftHandFlag ? 0b00010000 : 0b0;
            _data_state_flags |= sync.bounceFlag ? 0b00001000 : 0b0;
            _data_state_flags |= sync.sleepFlag ? 0b00000100 : 0b0;
            _pos = sync.pos;
            _rot = sync.rot;
            _vel = sync.vel;
            _spin = sync.spin;
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
