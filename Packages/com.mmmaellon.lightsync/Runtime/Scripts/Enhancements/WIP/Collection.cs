
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    [AddComponentMenu("Collection (lightsync)")]
    public class Collection : UdonSharpBehaviour
    {
        public Singleton singleton;
        [HideInInspector]
        public int collectionId = 1001;

        public DataList items = new DataList();
        public DataDictionary lookupTable = new DataDictionary();

        public CollectionItem[] startingItems;

        //temp stuff
        CollectionItem tempItem;
        bool success;

        public virtual bool Add(uint id)
        {
            success = AddToInternalList(id);
            if (success)
            {
                tempItem = singleton.collectionItems[id];
                tempItem._collectionId = collectionId;
                tempItem.data.collectionId = collectionId;
                tempItem.SyncIfOwner();
                OnAdded(tempItem);
                tempItem.OnAdded(this);
            }
            return success;
        }

        public virtual bool Add(CollectionItem item)
        {
            if (item == null)
            {
                return false;
            }
            success = AddToInternalList(item.itemId);
            if (success)
            {
                item._collectionId = collectionId;
                item.data.collectionId = collectionId;
                item.SyncIfOwner();
                OnAdded(item);
                item.OnAdded(this);
            }
            return success;
        }

        public virtual bool Remove(uint id)
        {
            success = RemoveFromInternalList(id);
            if (success)
            {
                tempItem = singleton.collectionItems[id];
                tempItem._collectionId = -1001;
                tempItem.data.collectionId = -1001;
                tempItem.SyncIfOwner();
                OnRemoved(singleton.collectionItems[id]);
                tempItem.OnRemoved(this);
            }
            return success;
        }

        public virtual bool Remove(CollectionItem item)
        {
            if (item == null)
            {
                return false;
            }
            success = RemoveFromInternalList(item.itemId);
            if (success)
            {
                item._collectionId = -1001;
                item.data.collectionId = -1001;
                item.SyncIfOwner();
                OnRemoved(item);
                item.OnRemoved(this);
            }
            return success;
        }

        public virtual void OnAdded(CollectionItem sync)
        { }

        public virtual void OnRemoved(CollectionItem sync)
        { }

        public bool AddToInternalList(uint id)
        {
            if (lookupTable.ContainsKey(id))
            {
                //already added
                return false;
            }

            lookupTable.Add(id, items.Count);
            items.Add(singleton.collectionItems[id]);
            return true;
        }

        public bool RemoveFromInternalList(uint id)
        {
            if (!lookupTable.ContainsKey(id))
            {
                //doesn't exist
                return false;
            }

            if (items.Count <= 1)
            {
                items.Clear();
                lookupTable.Clear();
            }
            else
            {
                uint removeIndex = lookupTable[id].UInt;
                CollectionItem lastItem = (CollectionItem)items[items.Count - 1].Reference;

                items.SetValue((int)removeIndex, lastItem);
                lookupTable.SetValue(lastItem.itemId, removeIndex);

                items.RemoveAt(items.Count - 1);
                lookupTable.Remove(id);
            }
            return true;
        }

        public virtual bool Contains(uint id)
        {
            return lookupTable.ContainsKey(id);
        }

    }
}
