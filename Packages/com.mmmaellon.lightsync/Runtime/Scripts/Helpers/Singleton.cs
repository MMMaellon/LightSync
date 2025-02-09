
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UdonSharpEditor;
using System.Linq;

#endif




#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
#endif

namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [ExecuteAlways]
    public class Singleton : UdonSharpBehaviour
    {
        public LightSync[] lightSyncs;


#if UNITY_EDITOR && !COMPILER_UDONSHARP

        public void AutoSetup(bool skipAlreadySetup = true)
        {
            for (uint i = 0; i < lightSyncs.Length; i++)
            {
                if (lightSyncs[i].id == i && lightSyncs[i].singleton == this && lightSyncs[i].AlreadySetup() && skipAlreadySetup)
                {
                    continue;
                }
                lightSyncs[i].id = i;
                lightSyncs[i].singleton = this;
                new SerializedObject(lightSyncs[i]).Update();
                if (PrefabUtility.IsPartOfPrefabInstance(lightSyncs[i]))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(lightSyncs[i]);
                }
                lightSyncs[i].AutoSetup();
            }
            if (PrefabUtility.IsPartOfPrefabInstance(this))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
        }

        // [MenuItem("MMMaellon/Test")]
        // public static void Test()
        // {
        //     Debug.LogWarning("Running TEST");
        //     Debug.LogWarning("Generating a random set of collections");
        //     Collection[] cols = new Collection[100];
        //     GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        //     var singleton = obj.AddComponent<Singleton>();
        //     for (int i = 0; i < cols.Length; i++)
        //     {
        //         cols[i] = obj.AddComponent<Collection>();
        //         cols[i].collectionId = Random.Range(0, 100);
        //     }
        //     var str = singleton.CollectionsToStr(cols);
        //     Debug.Log(str);
        //     GameObject.DestroyImmediate(obj);
        // }


        public static void DestroyEmptyGameObject(GameObject obj)
        {
            if (obj)
            {
                var comps = obj.GetComponents<Component>();
                var shouldDeleteGameObject = true;
                foreach (var c in comps)
                {
                    if (c.GetType() == typeof(Transform))
                    {
                        continue;
                    }
                    if (c.GetType() == typeof(UdonBehaviour))
                    {
                        var udon = (UdonBehaviour)c;
                        UdonSharpBehaviour udonSharpBehaviour = UdonSharpEditorUtility.GetProxyBehaviour(udon);
                        if (udonSharpBehaviour == null && UdonSharpEditorUtility.IsUdonSharpBehaviour(udon))
                        {
                            //udonbehaviour that is supposed to belong to an UdonSharpBehaviour, but the udonsharp doesn't exist.
                            continue;
                        }
                    }
                    shouldDeleteGameObject = false;
                    break;
                }
                if (shouldDeleteGameObject)
                {
                    DestroyImmediate(obj);
                }
            }
        }
#endif
    }
}
