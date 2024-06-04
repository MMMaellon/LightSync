
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class AttachToPlayerData : LightSyncEnhancementData
    {
        [System.NonSerialized, UdonSynced]
        public int playerId = 0;

        [System.NonSerialized, UdonSynced]
        public int bone = -1001;

        [System.NonSerialized, UdonSynced]
        public Vector3 position = Vector3.zero;

        [System.NonSerialized, UdonSynced]
        public Quaternion rotation = Quaternion.identity;

        public AttachToPlayer attach;

        public override void OnDeserialization()
        {
            attach.OnAttachDeserialized();
        }
    }
}
