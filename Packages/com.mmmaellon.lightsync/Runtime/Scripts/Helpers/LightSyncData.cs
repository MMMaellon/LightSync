using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UdonSharpEditor;
#endif


namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]//prevents it from showing up in the add component menu
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public abstract class LightSyncData : UdonSharpBehaviour
    {
        public LightSync sync;
        public override void OnPreSerialization()
        {
            serializationRequested = false;
            SyncNewData();
            if (sync.debugLogs)
            {
                sync._print("Sending: " + sync.prettyPrint());
            }
        }

        public float lastDeserialization;
        public override void OnDeserialization(VRC.Udon.Common.DeserializationResult result)
        {
            sync.CalcSmoothingTime(result.sendTime);
            if (!sync.allowOutOfOrderData && sync.localSyncCount > sync.syncCount && sync.localSyncCount - sync.syncCount < 8)//means we got updates out of order
            {
                sync._print("Rejecting Data");
                //revert all synced values
                RejectNewSyncData();
                return;
            }
            sync.StopLoop();
            AcceptNewSyncData();
            lastDeserialization = Time.timeSinceLevelLoad;
            sync.localSyncCount = sync.syncCount;

            if (sync.debugLogs)
            {
                sync._print("Receiving: " + sync.prettyPrint());
            }

            sync.StartLoop();
        }

        public abstract void RejectNewSyncData();

        public abstract void AcceptNewSyncData();

        public abstract void SyncNewData();


        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            sync.Owner = player;
            if (sync.separateHelperObjects && player.isLocal)
            {
                BubbleUpOwnership();
            }
            //we made need to do some special stuff here eventually
        }

        public virtual void BubbleUpOwnership()
        {
            if (Utilities.IsValid(sync.Owner) && !sync.Owner.IsOwner(sync.gameObject))
            {
                Networking.SetOwner(sync.Owner, sync.gameObject);
            }
        }

        bool syncRequested = false;
        bool serializationRequested = false;
        public virtual void RequestSync()
        {
            if (serializationRequested)
            {
                return;
            }
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
            serializationRequested = true;
            RequestSerialization();
        }

        public virtual void RequestSyncCallback()
        {
            if (!syncRequested)
            {
                return;
            }
            syncRequested = false;
            RequestSync();
        }

        [System.NonSerialized]
        public bool destroyCalled = false;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }
            RefreshHideFlags();
        }

        public void RefreshHideFlags()
        {
            if (!sync)
            {
                gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
                hideFlags &= ~HideFlags.HideInInspector;
            }
            else if (sync.data != this)
            {

                sync = null;
                DestroyAsync();
            }
            else
            {
                if (sync.showInternalObjects)
                {
                    gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
                    hideFlags &= ~HideFlags.HideInInspector;
                }
                else if (sync.separateHelperObjects)
                {
                    gameObject.hideFlags = HideFlags.HideInHierarchy;
                }
                else
                {
                    hideFlags |= HideFlags.HideInInspector;
                }
                return;
            }
        }

        public void DestroyAsync()
        {
            destroyCalled = true;
            if (sync && sync.data == this)
            {
                sync.data = null;
            }
            Invoke(nameof(Destroy), 0f);
        }

        public void Destroy()
        {
            gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
            hideFlags &= ~HideFlags.HideInInspector;
            var obj = gameObject;
            UdonSharpEditorUtility.DestroyImmediate(this);
            Singleton.DestroyEmptyGameObject(obj);
        }
#endif
    }
}
