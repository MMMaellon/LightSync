using System.Collections.Generic;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]//prevents it from showing up in the add component menu
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public abstract class LightSyncEnhancementData : UdonSharpBehaviour
    {
        public LightSyncEnhancementWithData enhancement;
        public override void OnDeserialization()
        {
            enhancement.OnDataDeserialization();
        }

        bool syncRequested = false;
        public virtual void RequestSync()
        {
            if (!Networking.LocalPlayer.IsOwner(gameObject))
            {
                syncRequested = false;
                return;
            }
            if (Networking.IsClogged)
            {
                if (!syncRequested)
                {
                    syncRequested = true;
                    SendCustomEventDelayedFrames(nameof(RequestSyncCallback), 5);
                }
                return;
            }
            RequestSerialization();
        }

        public virtual void RequestSyncCallback()
        {
            syncRequested = false;
            RequestSync();
        }
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public virtual void RefreshHideFlags()
        {
            if (enhancement && enhancement.sync && enhancement.enhancementData == this)
            {
                if (enhancement.sync.showInternalObjects)
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
                enhancement = null;
                DestroyAsync();
            }
        }

        public void DestroyAsync()
        {
            if (gameObject.activeInHierarchy && enabled)//prevents log spam in play mode
            {
                StartCoroutine(Destroy());
            }
        }

        public IEnumerator<WaitForSeconds> Destroy()
        {
            yield return new WaitForSeconds(0);
            DestroyImmediate(gameObject);
        }
#endif
    }
}
