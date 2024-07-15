
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
    public class ObjectPool : UdonSharpBehaviour
    {
        public bool AutoPopulateFromChildren = true;
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

        public ObjectPoolObject GetRandomHidden()
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
            tmpObj = GetRandomHidden();
            if (tmpObj)
            {
                tmpObj.Show();
            }
            return tmpObj;
        }

        public bool HideById(int index)
        {
            if (index < 0 || index >= objects.Length)
            {
                return false;
            }
            objects[index].Hide();
            return true;
        }
        public bool ShowById(int index)
        {
            if (index < 0 || index >= objects.Length)
            {
                return false;
            }
            objects[index].Show();
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
            objects = objects.Union(GetComponentsInChildren<ObjectPoolObject>(true)).Distinct().ToArray();
            for (int i = 0; i < objects.Length; i++)
            {
                objects[i].pool = this;
                objects[i].id = i;
                objects[i].data.hidden = objects[i].defaultHidden;
                objects[i].SetVisibility();
                PrefabUtility.RecordPrefabInstancePropertyModifications(objects[i]);
                new SerializedObject(objects[i]).Update();
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
