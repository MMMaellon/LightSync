
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    public class LightSyncPhysicsDispatcher : UdonSharpBehaviour
    {
        public LightSync sync;
        void FixedUpdate()
        {
            enabled = false;
            sync.OnPhysicsDispatch();
        }
        public void Dispatch()
        {
            enabled = true;
        }
        public void CancelDispatch()
        {
            enabled = false;
        }
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public void OnValidate()
        {
            RefreshHideFlags();
        }

        public void RefreshHideFlags()
        {
            if (sync != null)
            {
                if (sync.dispatcher == this)
                {
                    if (sync.showInternalObjects)
                    {
                        gameObject.hideFlags = HideFlags.None;
                    }
                    else
                    {
                        gameObject.hideFlags = HideFlags.HideInHierarchy;
                    }
                    return;
                }
                else
                {
                    sync = null;
                }
            }

            gameObject.hideFlags = HideFlags.None;
        }
#endif
    }
}
