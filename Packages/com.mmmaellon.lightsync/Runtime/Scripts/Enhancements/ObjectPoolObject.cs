
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
using System.Linq;
#endif

namespace MMMaellon.LightSync
{
    [AddComponentMenu("LightSync/ObjectPool Object (lightsync)")]
    public class ObjectPoolObject : LightSyncEnhancementWithData
    {
        [HideInInspector]
        public int id = -1001;
        [HideInInspector]
        public ObjectPool pool;
        [HideInInspector]
        public ObjectPoolObjectData data;

        public bool spawned
        {
            get => data.spawned;
            set
            {
                data.spawned = value;
            }
        }

        public bool defaultSpawned = false;

        public override string GetDataTypeName()
        {
            if (pool && pool.AdvancedSpawnTransformSyncing)
            {
                return "MMMaellon.LightSync.ObjectPoolObjectDataWithPosition";
            }
            return "MMMaellon.LightSync.ObjectPoolObjectData";
        }

        public override void OnDataDeserialization()
        {
            SetVisibility();
        }

        public override void OnDataObjectCreation(LightSyncEnhancementData enhancementData)
        {
            data = (ObjectPoolObjectData)enhancementData;
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            //do nothing, we intentionally let the data object have a diff owner
            //owner of the data object is who is requesting that the object be shown or hidden
        }

        public void SetVisibility()
        {
            if (data.spawned)
            {
                OnSpawnPoolObject();
            }
            else
            {
                OnDespawn();
            }
        }

        public virtual void Spawn()
        {
            data.Spawn();
            OnSpawnPoolObject();
        }

        public virtual void Spawn(Vector3 position, Quaternion rotation)
        {
            data.Spawn(position, rotation);
            OnSpawnPoolObject();
            sync.TeleportToWorldSpace(position, rotation, sync.sleepOnSpawn);
        }

        public virtual void DelayedDespawn(int delayFrames)
        {
            SendCustomEventDelayedFrames(nameof(DespawnIfNetworkUnclogged), delayFrames);
        }

        public virtual void DespawnIfNetworkUnclogged()
        {
            if (Networking.IsClogged)
            {
                SendCustomEventDelayedFrames(nameof(DespawnIfNetworkUnclogged), Random.Range(1, 10));
            }
            else
            {
                Despawn();
            }
        }

        public virtual void Despawn()
        {
            data.Despawn();
            OnDespawn();
        }

        DataToken tmpToken;
        DataToken tmpToken2;

        public virtual void OnSpawnPoolObject()
        {
            //move it first in case it's in a despawner
            transform.position = data.GetSpawnPos();
            transform.rotation = data.GetSpawnRot();
            gameObject.SetActive(true);
            if (!sync.separateHelperObjects)
            {
                if (sync.IsOwner())
                {
                    sync.TeleportToWorldSpace(data.GetSpawnPos(), data.GetSpawnRot(), sync.sleepOnSpawn);
                    if (pool.ForceOwnershipTransfer)
                    {
                        data.RequestOwnershipSync();
                    }
                }
            }
            else if (sync.sleepOnSpawn)
            {
                sync.EnsureSleep();
            }

            // Debug.LogWarning("ON SHOW");
            // for (int i = 0; i < pool.hiddenPoolIndexes.Count; i++)
            // {
            //     Debug.LogWarning(pool.hiddenPoolIndexes[i]);
            // }
            // var keys = pool.lookupTable.GetKeys();
            // for (int i = 0; i < keys.Count; i++)
            // {
            //     Debug.LogWarning(keys[i] + ":" + pool.lookupTable[keys[i]]);
            // }
            // Debug.LogWarning("Removing " + id);

            if (!pool.lookupTable.ContainsKey(id))
            {
                return;
            }
            //stupid bug.
            if (pool.hiddenPoolIndexes.Count <= 1)
            {
                pool.hiddenPoolIndexes.Clear();
                pool.lookupTable.Clear();
            }
            else
            {
                int removeIndex = pool.lookupTable[id].Int;
                int newValue = pool.hiddenPoolIndexes[pool.hiddenPoolIndexes.Count - 1].Int;
                // Debug.LogWarning("removeIndex " + removeIndex);
                pool.hiddenPoolIndexes.SetValue(removeIndex, newValue);
                pool.lookupTable.SetValue(newValue, removeIndex);

                pool.lookupTable.Remove(id);
                pool.hiddenPoolIndexes.RemoveAt(pool.hiddenPoolIndexes.Count - 1);
            }

            // for (int i = 0; i < pool.hiddenPoolIndexes.Count; i++)
            // {
            //     Debug.LogWarning(pool.hiddenPoolIndexes[i]);
            // }
            // keys = pool.lookupTable.GetKeys();
            // for (int i = 0; i < keys.Count; i++)
            // {
            //     Debug.LogWarning(keys[i] + ":" + pool.lookupTable[keys[i]]);
            // }

            //this part keeps all the indexes the same in the lookuptable
            // if (pool.hiddenPoolIndexes.Count > 0)
            // {
            //     tmpToken2 = pool.hiddenPoolIndexes[pool.hiddenPoolIndexes.Count - 1];
            //     pool.hiddenPoolIndexes.Insert(tmpToken.Int, pool.hiddenPoolIndexes[pool.hiddenPoolIndexes.Count - 1]);
            //     pool.hiddenPoolIndexes.RemoveAt(pool.hiddenPoolIndexes.Count - 1);
            //     pool.lookupTable.SetValue(tmpToken2, tmpToken);
            // }
        }

        public virtual void OnDespawn()
        {
            if (sync.pickup && sync.pickup.IsHeld)
            {
                sync.pickup.Drop();
            }
            gameObject.SetActive(false);

            // Debug.LogWarning("ON HIDE");
            // for (int i = 0; i < pool.hiddenPoolIndexes.Count; i++)
            // {
            //     Debug.LogWarning(pool.hiddenPoolIndexes[i]);
            // }
            // var keys = pool.lookupTable.GetKeys();
            // for (int i = 0; i < keys.Count; i++)
            // {
            //     Debug.LogWarning(keys[i] + ":" + pool.lookupTable[keys[i]]);
            // }

            if (sync.IsOwner())
            {
                sync.TeleportToWorldSpace(data.GetSpawnPos(), data.GetSpawnRot(), sync.sleepOnSpawn);
            }
            if (pool.lookupTable.ContainsKey(id))
            {
                return;
            }
            tmpToken2 = new DataToken(pool.lookupTable.Count);
            pool.hiddenPoolIndexes.Add(id);
            pool.lookupTable.Add(id, tmpToken2);
            // Debug.LogWarning("Adding " + id + ":" + tmpToken2);
            // for (int i = 0; i < pool.hiddenPoolIndexes.Count; i++)
            // {
            //     Debug.LogWarning(pool.hiddenPoolIndexes[i]);
            // }
            // keys = pool.lookupTable.GetKeys();
            // for (int i = 0; i < keys.Count; i++)
            // {
            //     Debug.LogWarning(keys[i] + ":" + pool.lookupTable[keys[i]]);
            // }
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        public override void Reset()
        {
            base.Reset();
            sync.separateHelperObjects = true;
        }
        public override void CreateDataObject()
        {
            bool isCorrectType = pool && (pool.AdvancedSpawnTransformSyncing == (enhancementData is ObjectPoolObjectDataWithPosition));
            if (!enhancementData || enhancementData.enhancement != this || !isCorrectType)
            {
                System.Type dataType = System.Type.GetType(GetDataTypeName());
                if (dataType == null || dataType.ToString() == "")
                {
                    Debug.LogWarning($"ERROR: Invalid type name from GetDataTypeName() of {GetType().FullName}. Make sure to include the full namespace");
                    return;
                }
                if (!dataType.IsSubclassOf(typeof(LightSyncEnhancementData)))
                {
                    Debug.LogWarning($"ERROR: {GetType().FullName} cannot be setup because {dataType.FullName} does not inherit from {typeof(LightSyncEnhancementData).FullName}");
                    return;
                }
                GameObject dataObject = new(name + "_enhancementData" + GUID.Generate());
                if (enhancementData)
                {
                    enhancementData.DestroyAsync();
                }
                enhancementData = UdonSharpComponentExtensions.AddUdonSharpComponent(dataObject, dataType).GetComponent<LightSyncEnhancementData>();
            }
            if (enhancementData)
            {
                GameObject dataObject = enhancementData.gameObject;
                if (!PrefabUtility.IsPartOfAnyPrefab(dataObject) && pool && dataObject.transform.parent != pool.transform)
                {
                    dataObject.transform.SetParent(pool.transform, false);
                }
                // dataObject.name = name + "_enhancementData";
                enhancementData.enhancement = this;
                enhancementData.RefreshHideFlags();
                OnDataObjectCreation(enhancementData);
            }
        }
#endif
    }
}
