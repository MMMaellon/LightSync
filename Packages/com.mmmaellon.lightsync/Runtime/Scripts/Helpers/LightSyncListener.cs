
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace MMMaellon.LightSync
{
    public abstract class LightSyncListener : UdonSharpBehaviour
    {

        public abstract void OnChangeState(LightSync sync, int oldStateID, int newStateID);
        public abstract void OnChangeOwner(LightSync sync, VRCPlayerApi oldOwner, VRCPlayerApi newOwner);
    }
}
