using UnityEngine;

namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]//prevents it from showing up in the add component menu
    public class LightSyncLooperUpdate : LightSyncLooper
    {
        public void Update()
        {
            Loop();
        }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public override void RefreshHideFlags()
        {
            if (!sync)
            {
                gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
                hideFlags &= ~HideFlags.HideInInspector;
            }
            else if (sync.looper != this)
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
