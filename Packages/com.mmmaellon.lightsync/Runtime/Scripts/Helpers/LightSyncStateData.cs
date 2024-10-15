using System.Collections.Generic;
using UnityEngine;
using UdonSharp;
using VRC.SDKBase;
using UdonSharpEditor;
namespace MMMaellon.LightSync
{
    public abstract class LightSyncStateData : UdonSharpBehaviour
    {
        public LightSyncStateWithData state;
        public override void OnDeserialization()
        {
            state.OnDataDeserialization();
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
            if (state && state.sync && state.stateData == this)
            {
                if (state.sync.showInternalObjects)
                {
                    gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
                }
                else
                {
                    gameObject.hideFlags |= HideFlags.HideInHierarchy;
                }
                return;
            }
            else
            {
                state = null;
                DestroyAsync();//can't delete synchronously in OnValidate
            }
        }

        public void DestroyAsync()
        {
            if (gameObject.activeInHierarchy && enabled) //prevents log spam in play mode
            {
                StartCoroutine(Destroy());
            }
        }

        public IEnumerator<WaitForSeconds> Destroy()
        {
            yield return new WaitForSeconds(0);
            var count = GetComponents(typeof(Component)).Length;
            gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
            hideFlags &= ~HideFlags.HideInInspector;
            var obj = gameObject;
            UdonSharpEditorUtility.DestroyImmediate(this);
            Singleton.DestroyEmptyGameObject(obj);
        }


#endif
    }
}
