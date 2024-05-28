
using System;
using UdonSharp;
using UnityEngine;

namespace MMMaellon.LightSync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class LightSyncDataUltra : LightSyncData
    {
        [UdonSynced(UdonSyncMode.None)]
        Vector3 _data_vel_flags;
        [UdonSynced(UdonSyncMode.None)]
        Vector3 _data_pos;
        [UdonSynced(UdonSyncMode.None)]
        Vector3 _data_rot_spin;

        Vector3 prev_data_flags_vel;
        Vector3 prev_data_pos;
        Vector3 prev_data_rot_spin;
        public override void RejectNewSyncData()
        {
            _data_pos = prev_data_pos;
            _data_rot_spin = prev_data_rot_spin;
            _data_vel_flags = prev_data_flags_vel;
        }

        Vector3 _rot_axis = Vector3.zero;
        int flag_bytes;
        public override void AcceptNewSyncData()
        {
            pos = _data_pos;
            _rot_axis.x = ExtractHalf(true, _data_rot_spin.x);
            _rot_axis.y = ExtractHalf(true, _data_rot_spin.y);
            _rot_axis.z = ExtractHalf(true, _data_rot_spin.z);
            rot = Quaternion.AngleAxis(_rot_axis.magnitude, _rot_axis.normalized).normalized;
            spin.x = ExtractHalf(false, _data_rot_spin.x);
            spin.y = ExtractHalf(false, _data_rot_spin.y);
            spin.z = ExtractHalf(false, _data_rot_spin.z);
            vel.x = ExtractHalf(true, _data_vel_flags.x);
            vel.y = _data_vel_flags.y;
            vel.z = ExtractHalf(false, _data_vel_flags.x);
            flag_bytes = BitConverter.SingleToInt32Bits(_data_vel_flags.z);
            state = (sbyte)(flag_bytes >> 24);
            syncCount = (byte)((flag_bytes >> 16) & 0xF);
            teleportCount = (byte)((flag_bytes >> 8) & 0xF);
            localTransformFlag = (flag_bytes & 0b10000000) != 0;
            kinematicFlag = (flag_bytes & 0b01000000) != 0;
            pickupableFlag = (flag_bytes & 0b00100000) != 0;
            leftHandFlag = (flag_bytes & 0b00010000) != 0;
            bounceFlag = (flag_bytes & 0b00001000) != 0;
            sleepFlag = (flag_bytes & 0b00000100) != 0;
            loopTimingFlag = flag_bytes & 0b00000011;

            prev_data_pos = _data_pos;
            prev_data_rot_spin = _data_rot_spin;
            prev_data_flags_vel = _data_vel_flags;
        }

        float magnitude;
        public override void SyncNewData()
        {
            IncrementSyncCounter();
            _data_pos = pos;
            rot.ToAngleAxis(out magnitude, out _rot_axis);
            _rot_axis *= magnitude;
            _data_rot_spin.x = CombineFloats(_rot_axis.x, spin.x);
            _data_rot_spin.y = CombineFloats(_rot_axis.y, spin.y);
            _data_rot_spin.z = CombineFloats(_rot_axis.z, spin.z);
            _data_vel_flags.x = CombineFloats(vel.x, vel.z);
            _data_vel_flags.y = vel.y;
            flag_bytes = (state << 24) | (syncCount << 16) | (teleportCount << 8) | loopTimingFlag;
            flag_bytes |= localTransformFlag ? 0b10000000 : 0b0;
            flag_bytes |= kinematicFlag ? 0b01000000 : 0b0;
            flag_bytes |= pickupableFlag ? 0b00100000 : 0b0;
            flag_bytes |= leftHandFlag ? 0b00010000 : 0b0;
            flag_bytes |= bounceFlag ? 0b00001000 : 0b0;
            flag_bytes |= sleepFlag ? 0b00000100 : 0b0;
            _data_vel_flags.z = BitConverter.Int32BitsToSingle(flag_bytes);

            prev_data_pos = _data_pos;
            prev_data_rot_spin = _data_rot_spin;
            prev_data_flags_vel = _data_vel_flags;
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

        public string BytesToStr(int bytes)
        {
            string str = "";
            for (int i = 31; i >= 0; i--)
            {
                if ((bytes & 1 << i) != 0)
                {
                    str += "1";
                }
                else
                {
                    str += "0";
                }
                if ((i % 8) == 0)
                {
                    str += " ";
                }
            }
            return str;
        }
    }
}
