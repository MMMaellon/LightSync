using System.Collections.Generic;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;


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
            if (sync.separateDataObject && player.isLocal)
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

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public void OnValidate()
        {
            RefreshHideFlags();
        }

        public void RefreshHideFlags()
        {
            if (sync != null)
            {
                if (sync.data == this)
                {
                    if (sync.showInternalObjects)
                    {
                        gameObject.hideFlags = HideFlags.None;
                    }
                    else if (sync.separateDataObject)
                    {
                        gameObject.hideFlags = HideFlags.HideInHierarchy;
                    }
                    else
                    {
                        hideFlags = HideFlags.HideInInspector;
                    }
                    return;
                }
                else
                {
                    sync = null;
                    DestroyAsync();
                }
            }

            gameObject.hideFlags = HideFlags.None;
        }

        public void DestroyAsync()
        {
            if (gameObject.activeInHierarchy)//prevents log spam in play mode
            {
                StartCoroutine(Destroy());
            }
        }

        public IEnumerator<WaitForSeconds> Destroy()
        {
            yield return new WaitForSeconds(0);
            var count = GetComponents(typeof(Component)).Length;
            gameObject.hideFlags = HideFlags.None;
            hideFlags = HideFlags.None;
            if (count > 3)
            {
                DestroyImmediate(this);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
#endif
    }
}
