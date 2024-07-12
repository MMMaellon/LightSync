
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

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
        public bool hidden
        {
            get => data.hidden;
            set
            {
                data.hidden = value;
                SetVisibility();
            }
        }

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

        public void SetVisibility()
        {
            if (data.hidden)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        DataToken tmpToken;
        DataToken tmpToken2;
        public virtual void Show()
        {
            if (!_hidden)
            {
                return;
            }
            _hidden = false;
            gameObject.SetActive(!_hidden);
            if (sync.IsOwner())
            {
                data.hidden = _hidden;
                data.RequestSerialization();
            }
            if (!pool.lookupTable.Remove(id, out tmpToken))
            {
                return;
            }

            //stupid bug. like seriously what the fuck
            Debug.LogWarning("count right now: " + pool.hiddenPoolIndexes.Count);
            if (pool.hiddenPoolIndexes.Count == 1)
            {
                pool.hiddenPoolIndexes.Clear();
            }
            else
            {
                Debug.LogWarning("removing " + tmpToken.Int);
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

        public virtual void Hide()
        {
            if (_hidden)
            {
                return;
            }
            _hidden = true;
            gameObject.SetActive(!_hidden);
            if (sync.IsOwner())
            {
                data.hidden = _hidden;
                data.RequestSerialization();
            }
            tmpToken = new DataToken(id);
            if (pool.lookupTable.ContainsKey(tmpToken))
            {
                return;
            }
            tmpToken2 = new DataToken(pool.lookupTable.Count);
            pool.hiddenPoolIndexes.Add(tmpToken);
            pool.lookupTable.Add(tmpToken, tmpToken2);
        }

        //we have a detached data object so we have to make sure it stays in sync with us
        public virtual void OnEnable()
        {
            if (Networking.LocalPlayer.IsOwner(gameObject) && !Networking.LocalPlayer.IsOwner(enhancementData.gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, enhancementData.gameObject);
            }
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
