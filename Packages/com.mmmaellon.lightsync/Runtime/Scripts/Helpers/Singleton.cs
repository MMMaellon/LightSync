﻿
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharpEditor;



#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
#endif

namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class Singleton : UdonSharpBehaviour
    {
        public LightSync[] lightSyncs;
        public CollectionItem[] collectionItems;
        public Collection[] collections;

        [UdonSynced]
        public string[] collectionSets = { };
        public DataDictionary collectionSetLUT = new DataDictionary();//Maps entries in CollectionSets back to their ID

        public int AddSet(Collection[] set)
        {
            var str = CollectionsToStr(set);
            if (collectionSetLUT.ContainsKey(str))
            {
                return collectionSetLUT[str].Int;
            }

            var index = collectionSets.Length;
            var newSets = new string[index + 1];
            System.Array.Copy(collectionSets, newSets, index);
            collectionSets = newSets;

            collectionSets[index] = str;
            collectionSetLUT.Add(str, index);

            return collectionSets.Length - 1;
        }

        public int GetSetId(Collection[] set)
        {
            var str = CollectionsToStr(set);
            if (collectionSetLUT.ContainsKey(str))
            {
                return collectionSetLUT[str].Int;
            }
            return -1001;
        }

        public string CollectionsToStr(Collection[] cols)
        {
            var str = "";
            cols = SortCollectionArray(cols);
            var lastID = -1001;
            foreach (var col in cols)
            {
                if (col.collectionId == lastID)
                {//check for duplicates and skip
                    continue;
                }
                lastID = col.collectionId;
                str += lastID.ToString() + ",";//will result in a trailing comma but who cares
            }
            return str;
        }

        public Collection[] SortCollectionArray(Collection[] cols)
        {
            recursiveArray = cols;
            SortCollectionArrayRecursive(0, cols.Length - 1);
            return recursiveArray;
        }

        Collection[] recursiveArray;
        [RecursiveMethod]
        void SortCollectionArrayRecursive(int startIndex, int endIndex)
        {
            if (startIndex >= endIndex || startIndex < 0 || endIndex > recursiveArray.Length - 1)
            { //if we're sorting an array of one or the indexes are invalid we return instantly
                return;
            }

            //randomly find the pivot
            var pivotIndex = Random.Range(startIndex, endIndex + 1);//+ 1 because random.range for ints has an exclusive end number
            var pivot = recursiveArray[pivotIndex];
            var pivotId = recursiveArray[pivotIndex].collectionId;
            var walkForward = startIndex;
            var walkBackward = endIndex - 1;

            //we don't want to sort the pivot so we replace it with the end index. end index is now the swap spot
            recursiveArray[pivotIndex] = recursiveArray[endIndex];

            //walk towards the middle from both ends
            while (walkForward < walkBackward)
            {
                if (recursiveArray[walkForward].collectionId < pivotId)
                {//this index is already correctly partitioned
                    walkForward++;
                }
                else if (recursiveArray[walkBackward].collectionId >= pivotId)
                { //this index is already correctly partitioned
                    walkBackward--;
                }
                else
                {
                    //both the forward and backward index are in the wrong position, so we need to do a swap
                    recursiveArray[endIndex] = recursiveArray[walkForward];
                    recursiveArray[walkForward] = recursiveArray[walkBackward];
                    recursiveArray[walkBackward] = recursiveArray[endIndex];
                    walkForward++;
                    walkBackward--;
                }
            }

            //walkForward and walkBackward are now equal and we can insert the pivot back in. To make room for it we move whatever's at the index we need to the end
            if (recursiveArray[walkForward].collectionId >= pivotId)
            {
                pivotIndex = walkForward;
            }
            else
            {
                pivotIndex = walkForward + 1;
            }
            recursiveArray[endIndex] = recursiveArray[pivotIndex];
            recursiveArray[pivotIndex] = pivot;
            SortCollectionArrayRecursive(startIndex, pivotIndex - 1);
            SortCollectionArrayRecursive(pivotIndex + 1, endIndex);
        }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public void AutoSetup()
        {
            for (int i = 0; i < collections.Length; i++)
            {
                collections[i].collectionId = i + 1;//1 indexed
                collections[i].singleton = this;
                new SerializedObject(collections[i]).Update();
                PrefabUtility.RecordPrefabInstancePropertyModifications(collections[i]);
            }
            for (uint i = 0; i < collectionItems.Length; i++)
            {
                collectionItems[i].itemId = i;
                collectionItems[i].singleton = this;
                collectionItems[i].SetupStartingCollections();
                new SerializedObject(collectionItems[i]).Update();
                PrefabUtility.RecordPrefabInstancePropertyModifications(collectionItems[i]);
            }
            for (uint i = 0; i < lightSyncs.Length; i++)
            {
                lightSyncs[i].id = i;
                lightSyncs[i].singleton = this;
                lightSyncs[i].AutoSetup();
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
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
                        UdonSharpBehaviour udonSharpBehaviour = UdonSharpEditorUtility.GetProxyBehaviour((UdonBehaviour)c);
                        if (udonSharpBehaviour == null && UdonSharpEditorUtility.IsUdonSharpBehaviour((UdonBehaviour)c))
                        {
                            //udonbehaviour with no udonsharp.
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
