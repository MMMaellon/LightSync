
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UdonSharpEditor;
using UnityEditor;
#endif

namespace MMMaellon.LightSync
{
    public class CollectionItem : LightSyncEnhancementWithData
    {
        public uint itemId = 1001;
        public CollectionItemData data;
        [SerializeField]
        Collection _collection = null;
        public Collection collection
        {
            get => _collection;
        }

        public int _collectionId = -1001;
        public int collectionId
        {
            get => _collectionId;
            set
            {
                if (collection)
                {
                    collection.RemoveFromInternalList(itemId);
                    collection.OnRemoved(this);
                    OnRemoved(collection);
                }
                _collectionId = value;
                if (_collectionId >= 0 && _collectionId < sync.singleton.collections.Length)
                {
                    _collection = sync.singleton.collections[_collectionId];
                }
                if (collection)
                {
                    collection.AddToInternalList(itemId);
                    collection.OnAdded(this);
                    OnAdded(collection);
                }
                SyncIfOwner();
            }
        }

        public void SyncIfOwner()
        {
            if (Networking.LocalPlayer.IsOwner(gameObject))
            {
                RequestSerialization();
            }
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (gameObject != data.gameObject && Utilities.IsValid(player) && player.isLocal)
            {
                Networking.SetOwner(player, data.gameObject);
            }
        }

        public virtual void OnAdded(Collection collection)
        { }

        public virtual void OnRemoved(Collection collection)
        { }

        public override void OnDataObjectCreation(LightSyncEnhancementData enhancementData)
        {
            data = (CollectionItemData)enhancementData;
        }
        public override void OnDataDeserialization()
        {
            collectionId = data.collectionId;
        }

        public override string GetDataTypeName()
        {
            return "MMMaellon.LightSync.CollectionItemData";
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR

        public override void CreateDataObject()
        {
            //check if we should delete the existing one and start over
            if (enhancementData)
            {
                var shouldDelete = false;
                if (enhancementData.enhancement != this)
                {
                    shouldDelete = true;
                }
                else if (sync.separateDataObject == (enhancementData.gameObject != sync.data.gameObject))
                {
                    shouldDelete = true;
                }

                if (shouldDelete)
                {
                    enhancementData.Destroy();
                }
            }

            //create a new object
            if (!enhancementData)
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
                GameObject dataObject = sync.data.gameObject;
                if (sync.separateDataObject)
                {
                    dataObject = new(name + "_collectionData_" + GUID.Generate().ToString());
                }
                enhancementData = UdonSharpComponentExtensions.AddUdonSharpComponent(dataObject, dataType).GetComponent<LightSyncEnhancementData>();
                OnDataObjectCreation(enhancementData);
            }

            //setup
            enhancementData.enhancement = this;
            enhancementData.RefreshHideFlags();
        }
#endif
    }
}
