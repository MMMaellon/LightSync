using UnityEngine;
namespace MMMaellon.LightSync
{
    public class LightSyncLooperFixedUpdate : LightSyncLooper
    {
        public void FixedUpdate()
        {
            Loop();
        }
        public override float GetAutoSmoothedInterpolation(float elapsedTime)
        {
            return data.autoSmoothingTime <= 0 ? 1 : (elapsedTime + Time.fixedTime) / data.autoSmoothingTime;
        }
    }
}
