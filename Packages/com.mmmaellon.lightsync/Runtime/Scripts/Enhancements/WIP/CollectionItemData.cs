
using MMMaellon.LightSync;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon
{
    [AddComponentMenu("")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class CollectionItemData : LightSyncEnhancementData
    {
        [UdonSynced]
        public int collectionId = -1001;
    }
}
