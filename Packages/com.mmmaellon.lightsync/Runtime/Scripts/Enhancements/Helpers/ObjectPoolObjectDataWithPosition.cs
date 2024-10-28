
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]
    public class ObjectPoolObjectDataWithPosition : ObjectPoolObjectData
    {
        [UdonSynced]
        Vector3 syncedSpawnPos;
        [UdonSynced]
        Quaternion syncedSpawnRot;

        public override Vector3 GetSpawnPos()
        {
            return syncedSpawnPos;
        }

        public override Quaternion GetSpawnRot()
        {
            return syncedSpawnRot;
        }

        public override void Spawn(Vector3 position, Quaternion rotation)
        {
            if (!Networking.LocalPlayer.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
            syncedSpawnPos = position;
            syncedSpawnRot = rotation;
            spawned = true;
            RequestSerialization();
        }
    }
}
