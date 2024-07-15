
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
    public class ObjectPoolObject : LightSyncEnhancementWithData
    {
        [HideInInspector]
        public int id = -1001;
        [HideInInspector]
        public ObjectPool pool;
        [HideInInspector]
        public ObjectPoolObjectData data;

        [SerializeField]
        bool _hidden = false;
        public bool defaultHidden = true;

        public override string GetDataTypeName()
        {
            return "MMMaellon.LightSync.ObjectPoolObjectData";
        }

        public override void OnDataDeserialization()
        {
            SetVisibility();
        }

        public override void OnDataObjectCreation(LightSyncEnhancementData enhancementData)
        {
            data = (ObjectPoolObjectData)enhancementData;
            _hidden = data.hidden;
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            //do nothing, we intentionally let the data object have a diff owner
            //owner of the data object is who is requesting that the object be shown or hidden
        }

        public void SetVisibility()
        {
            if (data.hidden)
            {
                OnHide();
            }
            else
            {
                OnShow();
            }
        }

        public virtual void Show()
        {
            data.Show();
            OnShow();
        }

        public virtual void Show(Vector3 position, Quaternion rotation)
        {
            data.Show(position, rotation);
            OnShow();
        }

        public virtual void Hide()
        {
            data.Hide();
            OnHide();
        }



        DataToken tmpToken;
        DataToken tmpToken2;

        public virtual void OnShow()
        {
            _hidden = false;
            transform.position = data.spawnPos;
            transform.rotation = data.spawnRot;
            gameObject.SetActive(!_hidden);
            if (sync.IsOwner())
            {
                sync.TeleportToWorldSpace(data.spawnPos, data.spawnRot, sync.sleepOnSpawn);
                if (pool.ForceOwnershipTransfer)
                {
                    data.RequestOwnershipSync();
                }
            }
            if (!pool.lookupTable.ContainsKey(id))
            {
                return;
            }
            tmpToken = pool.lookupTable[id];
            pool.lookupTable.Remove(id);
            //stupid bug.
            if (pool.hiddenPoolIndexes.Count == 1)
            {
                pool.hiddenPoolIndexes.Clear();
            }
            else
            {
                pool.hiddenPoolIndexes.RemoveAt(tmpToken.Int);
            }

            //this part keeps all the indexes the same in the lookuptable
            if (pool.hiddenPoolIndexes.Count > 0)
            {
                tmpToken2 = pool.hiddenPoolIndexes[pool.hiddenPoolIndexes.Count - 1];
                pool.hiddenPoolIndexes.Insert(tmpToken.Int, pool.hiddenPoolIndexes[pool.hiddenPoolIndexes.Count - 1]);
                pool.hiddenPoolIndexes.RemoveAt(pool.hiddenPoolIndexes.Count - 1);
                pool.lookupTable.SetValue(tmpToken2, tmpToken);
            }
        }

        public virtual void OnHide()
        {
            _hidden = true;
            if (sync.pickup && sync.pickup.IsHeld)
            {
                sync.pickup.Drop();
            }
            gameObject.SetActive(!_hidden);
            tmpToken = new DataToken(id);
            if (pool.lookupTable.ContainsKey(tmpToken))
            {
                return;
            }
            tmpToken2 = new DataToken(pool.lookupTable.Count);
            pool.hiddenPoolIndexes.Add(tmpToken);
            pool.lookupTable.Add(tmpToken, tmpToken2);
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        public override void CreateDataObject()
        {
            if (!enhancementData || enhancementData.enhancement != this)
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
                GameObject dataObject = new(name + "_enhancementData");
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
