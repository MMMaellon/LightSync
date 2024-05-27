using UnityEngine;
namespace MMMaellon.LightSync
{
    public class LightSyncLooperFixedUpdate : LightSyncLooper
    {
        public void FixedUpdate()
        {
            Loop();
        }
    }
}
