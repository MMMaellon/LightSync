using UnityEngine;
namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]//prevents it from showing up in the add component menu
    public class LightSyncLooperFixedUpdate : LightSyncLooper
    {
        public void FixedUpdate()
        {
            Loop();
        }
        // public override float GetAutoSmoothedInterpolation(float elapsedTime)
        // {
        //     return lerpPeriod <= 0 ? 1 : elapsedTime / lerpPeriod;
        // }
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public override void RefreshHideFlags()
        {
            if (!sync)
            {
                gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
                hideFlags &= ~HideFlags.HideInInspector;
            }
            else if (sync.fixedLooper != this)
            {
                sync = null;
                DestroyAsync();
            }
            else
            {
                base.RefreshHideFlags();
            }
        }
#endif
    }
}
