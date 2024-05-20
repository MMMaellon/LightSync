
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace MMMaellon.LightSync
{
    public abstract class LightSyncListener : UdonSharpBehaviour
    {

        public abstract void OnChangeState(LightSync sync, sbyte oldState, sbyte newState);
        public abstract void OnChangeOwner(LightSync sync, VRCPlayerApi oldOwner, VRCPlayerApi newOwner);
    }
}
