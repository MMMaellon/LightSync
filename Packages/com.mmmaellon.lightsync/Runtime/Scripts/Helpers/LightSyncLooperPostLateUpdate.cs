using UnityEngine;
namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]//prevents it from showing up in the add component menu
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
