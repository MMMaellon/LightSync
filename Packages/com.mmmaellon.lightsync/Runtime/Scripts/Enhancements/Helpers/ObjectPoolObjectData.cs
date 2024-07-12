
using MMMaellon.LightSync;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]//prevents it from showing up in the add component menu
    public class ObjectPoolObjectData : LightSyncEnhancementData
    {
        [UdonSynced]
        public bool hidden = true;
    }
}
