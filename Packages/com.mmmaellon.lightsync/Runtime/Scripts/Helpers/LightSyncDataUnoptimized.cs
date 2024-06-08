
using UdonSharp;
using UnityEngine;

namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]//prevents it from showing up in the add component menu
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class LightSyncDataUnoptimized : LightSyncData
    {
        [UdonSynced(UdonSyncMode.None)]
        sbyte _stateData = LightSync.STATE_PHYSICS;
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

        sbyte prevStateData = LightSync.STATE_PHYSICS;
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
            sync.state = _stateData;
            sync.syncCount = _syncCount;
            sync.teleportCount = _teleportCount;
            sync.localTransformFlag = _localTransformFlag;
            sync.leftHandFlag = _leftHandFlag;
            sync.kinematicFlag = _kinematicFlag;
            sync.pickupableFlag = _pickupableFlag;
            sync.bounceFlag = _bounceFlag;
            sync.sleepFlag = _sleepFlag;
            sync.pos = _pos;
            sync.rot = _rot;
            sync.vel = _vel;
            sync.spin = _spin;
            sync.loopTimingFlag = _loopTiming;

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
            sync.IncrementSyncCounter();
            _stateData = sync.state;
            _syncCount = sync.syncCount;
            _teleportCount = sync.teleportCount;
            _localTransformFlag = sync.localTransformFlag;
            _leftHandFlag = sync.leftHandFlag;
            _kinematicFlag = sync.kinematicFlag;
            _pickupableFlag = sync.pickupableFlag;
            _bounceFlag = sync.bounceFlag;
            _sleepFlag = sync.sleepFlag;
            _pos = sync.pos;
            _rot = sync.rot;
            _vel = sync.vel;
            _spin = sync.spin;
            _loopTiming = sync.loopTimingFlag;
        }
    }
}
