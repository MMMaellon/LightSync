
using MMMaellon.LightSync;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]//prevents it from showing up in the add component menu
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class ObjectPoolObjectData : LightSyncEnhancementData
    {
        [UdonSynced]
        public bool hidden = true;

        public Vector3 startSpawnPos;
        public Quaternion startSpawnRot;

        public virtual Vector3 GetSpawnPos()
        {
            return startSpawnPos;
        }

        public virtual Quaternion GetSpawnRot()
        {
            return startSpawnRot;
        }

        public virtual void SetSpawnPos(Vector3 pos)
        {
        }

        public virtual void SetSpawnRot(Quaternion rot)
        {
        }

        public virtual void Show()
        {
            Show(startSpawnPos, startSpawnRot);
        }

        public virtual void Show(Vector3 position, Quaternion rotation)
        {
            if (!Networking.LocalPlayer.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
            hidden = false;
            RequestSerialization();
        }

        public virtual void Hide()
        {
            if (!Networking.LocalPlayer.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
            hidden = true;
            RequestSerialization();
        }

        public void RequestOwnershipSync()
        {
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, nameof(SyncOwnership));
        }

        public void SyncOwnership()
        {
            Networking.SetOwner(Networking.LocalPlayer, enhancement.gameObject);
            enhancement.sync.Sync();
        }
    }
}
