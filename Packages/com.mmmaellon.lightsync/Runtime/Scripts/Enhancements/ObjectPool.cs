
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Data;
using VRC.Udon.Serialization.OdinSerializer;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System.Linq;
using UnityEditor;
using VRC.SDKBase.Editor.BuildPipeline;
#endif

namespace MMMaellon.LightSync
{
    [AddComponentMenu("LightSync/ObjectPool (lightsync)")]
    public class ObjectPool : UdonSharpBehaviour
    {
        public bool AutoPopulateFromChildren = true;
        [Header("Advanced Settings")]
        public bool ForceOwnershipTransfer = true;
        [Tooltip("Only needed if your lightsync data objects are parented. Costs some extra networking bandwidth.")]
        public bool AdvancedSpawnTransformSyncing = false;

        public ObjectPoolObject[] objects;
        [OdinSerialize]
        [HideInInspector]
        public DataList hiddenPoolIndexes;
        [OdinSerialize]
        [HideInInspector]
        public DataDictionary lookupTable;

        public int HiddenCount()
        {
            return hiddenPoolIndexes.Count;
        }

        public int TotalCount()
        {
            return objects.Length;
        }

        public ObjectPoolObject GetById(int index)
        {
            if (index < 0 || index >= objects.Length)
            {
                return null;
            }
            return objects[index];
        }

        public ObjectPoolObject GetRandomUnspawned()
        {
            if (HiddenCount() <= 0)
            {
                return null;
            }
            return objects[hiddenPoolIndexes[Random.Range(0, hiddenPoolIndexes.Count)].Int];
        }

        ObjectPoolObject tmpObj;
        public ObjectPoolObject SpawnRandom()
        {
            tmpObj = GetRandomUnspawned();
            if (tmpObj)
            {
                tmpObj.Spawn();
            }
            return tmpObj;
        }

        public ObjectPoolObject SpawnRandom(Vector3 position, Quaternion rotation)
        {
            tmpObj = GetRandomUnspawned();
            if (tmpObj)
            {
                tmpObj.Spawn(position, rotation);
            }
            return tmpObj;
        }

        public bool DespawnById(int index)
        {
            if (index < 0 || index >= objects.Length)
            {
                return false;
            }
            objects[index].Despawn();
            return true;
        }

        public void DespawnAll()
        {
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(DespawnAllOwned));
        }

        public void DespawnAllOwned()
        {
            foreach (var obj in objects)
            {
                if (!obj.spawned && Networking.LocalPlayer.IsOwner(obj.data.gameObject))
                {
                    obj.Despawn();
                }
            }
        }

        public bool SpawnById(int index)
        {
            if (index < 0 || index >= objects.Length)
            {
                return false;
            }
            objects[index].Spawn();
            return true;
        }

        public bool SpawnById(int index, Vector3 position, Quaternion rotation)
        {
            if (index < 0 || index >= objects.Length)
            {
                return false;
            }
            objects[index].Spawn(position, rotation);
            return true;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        public void OnValidate()
        {
            if (!AutoPopulateFromChildren)
            {
                return;
            }
            UpdateObjectList();
        }

        public void UpdateObjectList()
        {
            objects = objects.Union(GetComponentsInChildren<ObjectPoolObject>(true)).Distinct().Where((x) => x != null).ToArray();
            hiddenPoolIndexes.Clear();
            lookupTable.Clear();
            for (int i = 0; i < objects.Length; i++)
            {
                objects[i].pool = this;
                objects[i].id = i;
                objects[i].data.spawned = objects[i].defaultSpawned;
                objects[i].data.SetSpawnPos(objects[i].transform.position);
                objects[i].data.SetSpawnRot(objects[i].transform.rotation);
                objects[i].data.startSpawnPos = objects[i].transform.position;
                objects[i].data.startSpawnRot = objects[i].transform.rotation;
                objects[i].SetVisibility();
                PrefabUtility.RecordPrefabInstancePropertyModifications(objects[i]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(objects[i].data);
                new SerializedObject(objects[i]).Update();
                new SerializedObject(objects[i].data).Update();
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            new SerializedObject(this).Update();
        }
#endif
    }
#if !COMPILER_UDONSHARP && UNITY_EDITOR
    public class ObjectPoolBuilder : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => 1;
        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }
            AutoSetup();
        }

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            AutoSetup();
            return true;
        }

        public static void AutoSetup()
        {
            foreach (ObjectPool pool in GameObject.FindObjectsOfType<ObjectPool>(true))
            {
                if (!pool.AutoPopulateFromChildren)
                {
                    continue;
                }
                pool.UpdateObjectList();
            }
        }
    }
#endif
}
