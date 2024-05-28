using UnityEngine;

namespace MMMaellon.LightSync
{
    public class LightSyncDataDisabled : LightSyncData
    {
        sbyte _stateData = STATE_PHYSICS;
        byte _syncCount = 0;
        byte _teleportCount = 1;//start with a teleport
        bool _localTransformFlag = true;
        bool _leftHandFlag = false;
        bool _kinematicFlag = false;
        bool _pickupableFlag = true;
        bool _bounceFlag = false;
        bool _sleepFlag = true;
        Vector3 _pos = Vector3.zero;
        Quaternion _rot = Quaternion.identity;
        Vector3 _vel = Vector3.zero;
        Vector3 _spin = Vector3.zero;
        int _loopTiming;
        public override void RejectNewSyncData()
        {

        }

        public override void AcceptNewSyncData()
        {
            state = _stateData;
            syncCount = _syncCount;
            teleportCount = _teleportCount;
            localTransformFlag = _localTransformFlag;
            leftHandFlag = _leftHandFlag;
            kinematicFlag = _kinematicFlag;
            pickupableFlag = _pickupableFlag;
            bounceFlag = _bounceFlag;
            sleepFlag = _sleepFlag;
            pos = _pos;
            rot = _rot;
            vel = _vel;
            spin = _spin;
            loopTimingFlag = _loopTiming;
        }

        public override void SyncNewData()
        {

        }
    }
}
