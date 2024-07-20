
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
        Vector3 spawnPos;
        [UdonSynced]
        Quaternion spawnRot;

        public override Vector3 GetSpawnPos()
        {
            return spawnPos;
        }

        public override Quaternion GetSpawnRot()
        {
            return spawnRot;
        }

        public override void Spawn(Vector3 position, Quaternion rotation)
        {
            if (!Networking.LocalPlayer.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
            spawned = true;
            spawnPos = position;
            spawnRot = rotation;
            RequestSerialization();
        }
    }
}
