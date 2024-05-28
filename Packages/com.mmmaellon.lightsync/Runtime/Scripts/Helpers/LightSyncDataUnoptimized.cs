
using UdonSharp;
using UnityEngine;

namespace MMMaellon.LightSync
{
    public class LightSyncDataUnoptimized : LightSyncData
    {
        [UdonSynced(UdonSyncMode.None)]
        sbyte _stateData = STATE_PHYSICS;
        [UdonSynced(UdonSyncMode.None)]
        byte _syncCount = 0;
        [UdonSynced(UdonSyncMode.None)]
        byte _teleportCount = 1;//start with a teleport
        [UdonSynced(UdonSyncMode.None)]
        bool _localTransformFlag = true;
        [UdonSynced(UdonSyncMode.None)]
        bool _leftHandFlag = false;
        [UdonSynced(UdonSyncMode.None)]
        bool _kinematicFlag = false;
        [UdonSynced(UdonSyncMode.None)]
        bool _pickupableFlag = true;
        [UdonSynced(UdonSyncMode.None)]
        bool _bounceFlag = false;
        [UdonSynced(UdonSyncMode.None)]
        bool _sleepFlag = true;
        [UdonSynced(UdonSyncMode.None)]
        Vector3 _pos = Vector3.zero;
        [UdonSynced(UdonSyncMode.None)]
        Quaternion _rot = Quaternion.identity;
        [UdonSynced(UdonSyncMode.None)]
        Vector3 _vel = Vector3.zero;
        [UdonSynced(UdonSyncMode.None)]
        Vector3 _spin = Vector3.zero;
        [UdonSynced(UdonSyncMode.None)]
        int _loopTiming;

        sbyte prevStateData = STATE_PHYSICS;
        byte prevSyncCount = 0;
        byte prevTeleportCount = 1;//start with a teleport
        bool prevLocalTransformFlag = true;
        bool prevLeftHandFlag = false;
        bool prevKinematicFlag = false;
        bool prevPickupableFlag = true;
        bool prevBounceFlag = false;
        bool prevSleepFlag = true;
        Vector3 prevPos = Vector3.zero;
        Quaternion prevRot = Quaternion.identity;
        Vector3 prevVel = Vector3.zero;
        Vector3 prevSpin = Vector3.zero;
        int prevLoopTiming;
        public override void RejectNewSyncData()
        {
            _stateData = prevStateData;
            _syncCount = prevSyncCount;
            _teleportCount = prevTeleportCount;
            _localTransformFlag = prevLocalTransformFlag;
            _leftHandFlag = prevLeftHandFlag;
            _kinematicFlag = prevKinematicFlag;
            _pickupableFlag = prevPickupableFlag;
            _bounceFlag = prevBounceFlag;
            _sleepFlag = prevSleepFlag;
            _pos = prevPos;
            _rot = prevRot;
            _vel = prevVel;
            _spin = prevSpin;
            _loopTiming = prevLoopTiming;
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

            prevStateData = _stateData;
            prevSyncCount = _syncCount;
            prevTeleportCount = _teleportCount;
            prevLocalTransformFlag = _localTransformFlag;
            prevLeftHandFlag = _leftHandFlag;
            prevKinematicFlag = _kinematicFlag;
            prevPickupableFlag = _pickupableFlag;
            prevBounceFlag = _bounceFlag;
            prevSleepFlag = _sleepFlag;
            prevPos = _pos;
            prevRot = _rot;
            prevVel = _vel;
            prevSpin = _spin;
            prevLoopTiming = _loopTiming;
        }

        public override void SyncNewData()
        {
            IncrementSyncCounter();
            _stateData = state;
            _syncCount = syncCount;
            _teleportCount = teleportCount;
            _localTransformFlag = localTransformFlag;
            _leftHandFlag = leftHandFlag;
            _kinematicFlag = kinematicFlag;
            _pickupableFlag = pickupableFlag;
            _bounceFlag = bounceFlag;
            _sleepFlag = sleepFlag;
            _pos = pos;
            _rot = rot;
            _vel = vel;
            _spin = spin;
            _loopTiming = loopTimingFlag;
        }
    }
}
