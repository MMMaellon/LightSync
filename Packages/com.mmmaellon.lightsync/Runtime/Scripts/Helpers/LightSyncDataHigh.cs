using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    public class LightSyncDataHigh : LightSyncData
    {
        [System.NonSerialized, UdonSynced(UdonSyncMode.None)]
        int _data_state_flags;
        [System.NonSerialized, UdonSynced(UdonSyncMode.None)]
        Vector3 _pos = Vector3.zero;
        [System.NonSerialized, UdonSynced(UdonSyncMode.None)]
        Vector3 _data_rot_spin = Vector3.zero;
        [System.NonSerialized, UdonSynced(UdonSyncMode.None)]
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
            pos = _pos;
            vel = _vel;
            _rot_axis.x = ExtractHalf(true, _data_rot_spin.x);
            _rot_axis.y = ExtractHalf(true, _data_rot_spin.y);
            _rot_axis.z = ExtractHalf(true, _data_rot_spin.z);
            rot = Quaternion.AngleAxis(_rot_axis.magnitude, _rot_axis.normalized).normalized;
            spin.x = ExtractHalf(false, _data_rot_spin.x);
            spin.y = ExtractHalf(false, _data_rot_spin.y);
            spin.z = ExtractHalf(false, _data_rot_spin.z);
            _state = (sbyte)(_data_state_flags >> 24);
            syncCount = (byte)((_data_state_flags >> 16) & 0xF);
            teleportCount = (byte)((_data_state_flags >> 8) & 0xF);
            localTransformFlag = (_data_state_flags & 0b10000000) != 0;
            kinematicFlag = (_data_state_flags & 0b01000000) != 0;
            pickupableFlag = (_data_state_flags & 0b00100000) != 0;
            leftHandFlag = (_data_state_flags & 0b00010000) != 0;
            bounceFlag = (_data_state_flags & 0b00001000) != 0;
            sleepFlag = (_data_state_flags & 0b00000100) != 0;

            prevPos = _pos;
            prevVel = _vel;
            prevData = _data_state_flags;
            prev_data_rot_spin = _data_rot_spin;
        }

        public override void OnPreSerialization()
        {
            IncrementSyncCounter();
            _data_state_flags = (_state << 24) | (syncCount << 16) | (teleportCount << 8);
            _data_state_flags |= localTransformFlag ? 0b10000000 : 0b0;
            _data_state_flags |= kinematicFlag ? 0b01000000 : 0b0;
            _data_state_flags |= pickupableFlag ? 0b00100000 : 0b0;
            _data_state_flags |= leftHandFlag ? 0b00010000 : 0b0;
            _data_state_flags |= bounceFlag ? 0b00001000 : 0b0;
            _data_state_flags |= sleepFlag ? 0b00000100 : 0b0;
            _pos = pos;
            _vel = vel;
            _data_rot_spin.x = CombineFloats(rot.x, spin.x);
            _data_rot_spin.y = CombineFloats(rot.y, spin.y);
            _data_rot_spin.z = CombineFloats(rot.z, spin.z);

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
