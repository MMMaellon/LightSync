using UnityEngine;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
#endif
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

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public override void RefreshHideFlags()
        {
            if (!sync)
            {
                gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
                hideFlags &= ~HideFlags.HideInInspector;
            }
            else if (sync.lateLooper != this)
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
