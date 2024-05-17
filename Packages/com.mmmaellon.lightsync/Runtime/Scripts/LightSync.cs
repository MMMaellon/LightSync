
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LightSync : UdonSharpBehaviour
    {
        public LightSyncData data;//Gets created in the editor, as an invisible child of this object. Because it's a separate object we don't get issues with ownership and dumb stuff like that.

        public void OnSendData()
        {

        }

        public void OnReceiveData(VRC.Udon.Common.DeserializationResult result)
        {

        }
    }
}
