using System;
using UdonSharp;
using UnityEngine;

namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]//prevents it from showing up in the add component menu
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class LightSyncDataHigh : LightSyncData
    {
        [UdonSynced(UdonSyncMode.None)]
        int _data_state_flags;
        [UdonSynced(UdonSyncMode.None)]
        Vector3 _pos = Vector3.zero;
        [UdonSynced(UdonSyncMode.None)]
        Vector3 _data_rot_spin = Vector3.zero;
        [UdonSynced(UdonSyncMode.None)]
        Vector3 _vel = Vector3.zero;

        int prevData;
        Vector3 prevPos;
        Vector3 prev_data_rot_spin;
        Vector3 prevVel;
        Vector3 _rot_axis;
        public override void RejectNewSyncData()
        {
            _pos = prevPos;
            _vel = prevVel;
            _data_rot_spin = prev_data_rot_spin;
            _data_state_flags = prevData;
        }

        public override void AcceptNewSyncData()
        {
            sync.pos = _pos;
            sync.vel = _vel;
            _rot_axis.x = ExtractHalf(true, _data_rot_spin.x);
            _rot_axis.y = ExtractHalf(true, _data_rot_spin.y);
            _rot_axis.z = ExtractHalf(true, _data_rot_spin.z);
            sync.rot = Quaternion.AngleAxis(_rot_axis.magnitude, _rot_axis.normalized).normalized;
            sync.spin.x = ExtractHalf(false, _data_rot_spin.x);
            sync.spin.y = ExtractHalf(false, _data_rot_spin.y);
            sync.spin.z = ExtractHalf(false, _data_rot_spin.z);
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
            prevVel = _vel;
            prevData = _data_state_flags;
            prev_data_rot_spin = _data_rot_spin;
        }

        float magnitude;
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
            _vel = sync.vel;
            sync.rot.ToAngleAxis(out magnitude, out _rot_axis);
            _rot_axis *= magnitude;
            _data_rot_spin.x = CombineFloats(_rot_axis.x, sync.spin.x);
            _data_rot_spin.y = CombineFloats(_rot_axis.y, sync.spin.y);
            _data_rot_spin.z = CombineFloats(_rot_axis.z, sync.spin.z);

            prevPos = _pos;
            prevVel = _vel;
            prevData = _data_state_flags;
            prev_data_rot_spin = _data_rot_spin;
        }

        const float halfMultiplier = 90f;
        static float ExtractHalf(bool firstHalf, float number)
        {
            int intBits;
            if (firstHalf)
            {
                intBits = (BitConverter.SingleToInt32Bits(number) >> 16) & 0xFFFF;
            }
            else
            {
                intBits = BitConverter.SingleToInt32Bits(number) & 0xFFFF;
            }
            if ((intBits & 0b1000000000000000) != 0)
            {
                //check for negative bit since these bits technically represent a short
                //-65536 is 0xFFFF0000
                intBits = -65536 | intBits;
            }
            return intBits / halfMultiplier;
        }

        static float CombineFloats(float firstHalf, float secondHalf)
        {
            return BitConverter.Int32BitsToSingle
                (
                    (Mathf.RoundToInt(Mathf.Clamp(firstHalf * halfMultiplier, short.MinValue, short.MaxValue)) & 0xFFFF) << 16
                    | (
                      Mathf.RoundToInt(Mathf.Clamp(secondHalf * halfMultiplier, short.MinValue, short.MaxValue)) & 0xFFFF
                    )
                );
        }

        // public void Start()
        // {
        //     Debug.LogWarning("DEBUG TEST");
        //
        //     _state = STATE_HELD;
        //     syncCount = 69;
        //     teleportCount = 12;
        //     localTransformFlag = true;
        //     kinematicFlag = true;
        //     pickupableFlag = true;
        //     leftHandFlag = true;
        //     bounceFlag = true;
        //     sleepFlag = true;
        //
        //     OnPreSerialization();
        //     AcceptNewSyncData();
        //
        //     sync._print(prettyPrint());
        // }
    }
}
