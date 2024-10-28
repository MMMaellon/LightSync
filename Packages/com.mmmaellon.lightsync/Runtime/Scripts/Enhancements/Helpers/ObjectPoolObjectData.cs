
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
        [UdonSynced, FieldChangeCallback(nameof(spawned))]
        public bool _spawned = false;
        public bool spawned
        {
            get => _spawned;
            set
            {
                _spawned = value;
                if (!value && enhancement.sync.pickup && enhancement.sync.pickup.IsHeld)
                {
                    enhancement.sync.pickup.Drop();
                }
                enhancement.gameObject.SetActive(value);
                if (enhancement.sync.IsOwner())
                {
                    if (value)
                    {
                        enhancement.sync.TeleportToWorldSpace(spawnPos, spawnRot, enhancement.sync.sleepOnSpawn);
                    }
                    RequestSerialization();
                }
                else
                {
                    //if the teleport data comes in before the object gets enabled
                    enhancement.sync.StartLoop();
                }
                if (value)
                {
                    poolObject.OnSpawnPoolObject();
                    SendCustomEventDelayedSeconds(nameof(VibeCheck), 2f);
                }
                else
                {
                    poolObject.transform.position = startSpawnPos;
                    poolObject.transform.rotation = startSpawnRot;
                    poolObject.OnDespawnPoolObject();
                }
            }
        }

        public void VibeCheck()
        {
            Debug.LogWarning("VIBE CHECK");
            Debug.LogWarning("enhancement.sync.position: " + enhancement.sync.pos);
            Debug.LogWarning("actual position: " + enhancement.transform.localPosition);
        }

        public Vector3 startSpawnPos;
        public Quaternion startSpawnRot;
        Vector3 spawnPos;
        Quaternion spawnRot;

        public ObjectPoolObject _poolObject;
        public ObjectPoolObject poolObject
        {
            get
            {
                if (!_poolObject)
                {
                    _poolObject = (ObjectPoolObject)enhancement;
                }
                return _poolObject;
            }
        }

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
            spawnPos = pos;
        }

        public virtual void SetSpawnRot(Quaternion rot)
        {
            spawnRot = rot;
        }

        public virtual void Spawn()
        {
            Spawn(startSpawnPos, startSpawnRot);
        }

        public virtual void Spawn(Vector3 position, Quaternion rotation)
        {
            if (!Networking.LocalPlayer.IsOwner(gameObject) || !Networking.LocalPlayer.IsOwner(enhancement.gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                Networking.SetOwner(Networking.LocalPlayer, enhancement.gameObject);
            }
            SetSpawnPos(position);
            SetSpawnRot(rotation);
            spawned = true;
        }

        public virtual void Despawn()
        {
            if (!Networking.LocalPlayer.IsOwner(gameObject) || !Networking.LocalPlayer.IsOwner(enhancement.gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                Networking.SetOwner(Networking.LocalPlayer, enhancement.gameObject);
            }
            spawned = false;
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
