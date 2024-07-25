
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]
    public class Singleton : UdonSharpBehaviour
    {
        public LightSync[] lightSyncs;
        public CollectionItem[] collectionItems;
        public Collection[] collections;
    }
}
