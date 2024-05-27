using UnityEngine;
namespace MMMaellon.LightSync
{
    public class LightSyncLooperPostLateUpdate : LightSyncLooper
    {
        public override void PostLateUpdate()
        {
            Loop();
        }

        public void Start()
        {
            //necessary for enabled checkbox
        }
    }
}
