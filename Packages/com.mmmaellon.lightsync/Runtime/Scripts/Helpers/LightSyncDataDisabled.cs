using UdonSharp;
using UnityEngine;

namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]//prevents it from showing up in the add component menu
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LightSyncDataDisabled : LightSyncData
    {
        sbyte _stateData = LightSync.STATE_PHYSICS;
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
        }

        public override void SyncNewData()
        {

        }
    }
}
