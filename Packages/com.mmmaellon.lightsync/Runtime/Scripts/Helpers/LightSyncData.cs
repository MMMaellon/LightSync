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
            SyncNewData();
            sync._print("SENDING DATA: " + sync.prettyPrint());
        }

        public override void OnDeserialization(VRC.Udon.Common.DeserializationResult result)
        {
            sync.CalcSmoothingTime(result.sendTime);
            if (sync.localSyncCount > sync.syncCount && sync.localSyncCount - sync.syncCount < 8)//means we got updates out of order
            {
                //revert all synced values
                sync._print("Out of order network packet received");
                RejectNewSyncData();
                return;
            }
            sync.StopLoop();
            AcceptNewSyncData();
            sync.localSyncCount = sync.syncCount;

            sync._print("NEW DATA: " + sync.prettyPrint());
            sync.StartLoop();
        }

        public abstract void RejectNewSyncData();

        public abstract void AcceptNewSyncData();

        public abstract void SyncNewData();

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            sync.Owner = player;
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
                    else
                    {
                        gameObject.hideFlags = HideFlags.HideInHierarchy;
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
