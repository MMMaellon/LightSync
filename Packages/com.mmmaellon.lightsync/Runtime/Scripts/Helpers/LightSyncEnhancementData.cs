using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System.Collections.Generic;
using UdonSharpEditor;
#endif

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
                enhancement = null;
                DestroyAsync(); //destroying in onvalidate is causing problems...
            }
        }

        public void DestroyAsync()
        {
            Invoke(nameof(Destroy), 0f);
        }

        public void Destroy()
        {
            gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
            hideFlags &= ~HideFlags.HideInInspector;
            var obj = gameObject;
            UdonSharpEditorUtility.DestroyImmediate(this);
            if(this)
            {
                DestroyImmediate(this, true);
            }
            Singleton.DestroyEmptyGameObject(obj);
        }
#endif
    }
}
