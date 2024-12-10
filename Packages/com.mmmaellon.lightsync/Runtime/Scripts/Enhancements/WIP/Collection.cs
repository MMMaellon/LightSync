
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

        [HideInInspector]
        public DataList items = new DataList();
        [HideInInspector]
        public DataDictionary lookupTable = new DataDictionary();

        public CollectionItem[] startingItems
        {
            get => _startingItems;
            set
            {
                _startingItems = value;
                if (singleton)
                {
                    singleton.collectionMembershipDirty = true;
                }
            }
        }
        public CollectionItem[] _startingItems;

        [HideInInspector]
        public DataList setIds = new DataList();
        [HideInInspector]
        public DataDictionary setIdLUT = new DataDictionary();
        public void AddToSet(int newSetId)
        {
            if (setIdLUT.ContainsKey(newSetId))
            {
                return;
            }
            setIds.Add(newSetId);
            setIdLUT.Add(newSetId, setIds.Count - 1);
        }

        public bool IsPartOfSet(int setId)
        {
            return setIdLUT.ContainsKey(setId);
        }

        public bool Add(CollectionItem item)
        {
            item.AddToCollection(this);
            return true;
        }

        public bool Remove(CollectionItem item)
        {
            item.RemoveFromCollection(this);
            return true;
        }

        public bool Contains(CollectionItem item)
        {
            if (!item)
            {
                return false;
            }
            return item.IsInCollection(this);
        }


        public virtual void OnAdded(CollectionItem item)
        { }

        public virtual void OnRemoved(CollectionItem item)
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
    }
}
