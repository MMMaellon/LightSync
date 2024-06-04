
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace MMMaellon.LightSync
{
    [RequireComponent(typeof(LightSync))]
    public abstract class LightSyncStateWithData : LightSyncState
    {
        public LightSyncStateData stateData;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        public override void Reset()
        {
            base.Reset();
            AutoSetup();
        }

        public void OnValidate()
        {
            if (gameObject.activeInHierarchy && enabled)//check is here to prevent Unity Editor error spam
            {
                StartCoroutine(AutoSetup());
            }
        }
        public void OnDestroy()
        {
            if (stateData)
            {
                stateData.StartCoroutine(stateData.Destroy());
            }
        }

        public IEnumerator<WaitForSeconds> AutoSetup()
        {
            yield return new WaitForSeconds(0);
            if (!sync)
            {
                yield break;
            }
            CreateDataObject();
        }

        public virtual void CreateDataObject()
        {
            if (!stateData || stateData.state != this)
            {
                GameObject dataObject = new(name + "_statedata" + stateID);
                dataObject.transform.SetParent(transform, false);
                stateData = dataObject.AddComponent<LightSyncStateData>();
                stateData.state = this;
            }
            if (stateData)
            {
                stateData.gameObject.name = new(name + "_statedata" + stateID);
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

        public virtual void OnEnable()
        {
            Networking.SetOwner(Networking.GetOwner(gameObject), stateData.gameObject);
        }
    }
}
