
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace MMMaellon.LightSync
{
    [RequireComponent(typeof(LightSync))]
    public abstract class LightSyncStateWithData : LightSyncState
    {
        public LightSyncStateData stateData;

        public abstract LightSyncStateData CreateDataObject(GameObject dataObject);

#if !COMPILER_UDONSHARP && UNITY_EDITOR

        public void OnDestroy()
        {
            if (stateData)
            {
                stateData.DestroyAsync();
            }
        }

        public override void AutoSetup()
        {
            base.AutoSetup();
            CreateDataObject();
        }

        public virtual void CreateDataObject()
        {
            if (!stateData || stateData.state != this)
            {
                GameObject dataObject = new(name + "_statedata" + stateID);
                dataObject.transform.SetParent(transform, false);
                stateData = CreateDataObject(dataObject);
                if (stateData)
                {
                    stateData.state = this;
                }
            }
            if (stateData)
            {
                stateData.gameObject.name = name + "_statedata" + stateID;
                stateData.RefreshHideFlags();
            }
        }
#endif
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && player.isLocal)
            {
                Networking.SetOwner(player, stateData.gameObject);
            }
        }

        // public virtual void OnEnable()
        // {
        //     Networking.SetOwner(Networking.GetOwner(gameObject), stateData.gameObject);
        // }
    }
}
