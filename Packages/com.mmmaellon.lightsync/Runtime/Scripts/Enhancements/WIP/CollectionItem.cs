
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon.Serialization.OdinSerializer;

namespace MMMaellon.LightSync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class CollectionItem : LightSyncEnhancement
    {
        [HideInInspector]
        public Singleton singleton;
        public uint itemId = 1001;

        public Collection[] startingCollections
        {
            get => _startingCollections;
            set
            {
                _startingCollections = value;
                if (singleton)
                {
                    singleton.collectionMembershipDirty = true;
                }
            }
        }
        public Collection[] _startingCollections = new Collection[0];

        [HideInInspector, OdinSerialize]
        public DataList collections = new DataList();//a list of all collections that this item is a member of

        public Collection[] GetCollectionsArray()
        {
            var arr = new Collection[collections.Count];
            for (int i = 0; i < collections.Count; i++)
            {
                arr[i] = (Collection)collections[i].Reference;
            }
            return arr;
        }

        [System.NonSerialized, UdonSynced, FieldChangeCallback(nameof(setId))]
        int _setId = -1001;
        public int setId
        {
            get => _setId;
            set
            {
                _setId = value;
                SyncIfOwner();
            }
        }

        [System.NonSerialized]
        public bool activeRequest;
        public bool IsInCollection(Collection col)
        {
            if (activeRequest)
            {
                return col.lookupTable.ContainsKey(itemId);
            }
            else if (col)
            {
                return col.IsPartOfSet(setId);
            }
            return false;
        }

        public bool AddToCollection(Collection col)
        {
            if (IsInCollection(col))
            {
                return false;
            }

            collections.Add(col);
            col.AddToInternalList(itemId);

            return true;
        }

        public bool RemoveFromCollection(Collection col)
        {
            if (!IsInCollection(col))
            {
                return false;
            }

            collections.Remove(col);
            col.RemoveFromInternalList(itemId);

            return true;
        }

        public void SyncIfOwner()
        {

#if UNITY_EDITOR && !COMPILER_UDONSHARP
            return;
#endif
            if (Networking.LocalPlayer.IsOwner(gameObject))
            {
                RequestSerialization();
            }
        }

        string collectionString;
        public string GetCollectionListString()
        {
            collectionString = "";
            //assumes that collections is sorted and sanitized to provide consistent strings
            foreach (Collection col in GetCollectionsArray())
            {
                if (collectionString.Length == 0)
                {
                    collectionString = $"{col.collectionId}";
                }
                else
                {
                    collectionString += $",{col.collectionId}";
                }
            }
            return collectionString;
        }
    }
}
