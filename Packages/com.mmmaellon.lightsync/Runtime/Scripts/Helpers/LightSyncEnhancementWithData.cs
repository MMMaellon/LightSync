
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace MMMaellon.LightSync
{
    [RequireComponent(typeof(LightSync))]
    public abstract class LightSyncEnhancementWithData : LightSyncEnhancement
    {
        public LightSyncEnhancementData _data;

        public abstract void OnDataObjectCreated(LightSyncEnhancementData dataObject);

#if !COMPILER_UDONSHARP && UNITY_EDITOR

        public override void Reset()
        {
            base.Reset();
            AutoSetup();
        }

        public void OnValidate()
        {
            AutoSetupAsync();
        }

        public void OnDestroy()
        {
            if (_data)
            {
                _data.DestroyAsync();
            }
        }
        public void AutoSetupAsync()
        {
            if (gameObject.activeInHierarchy && enabled)//prevents log spam in play mode
            {
                StartCoroutine(AutoSetup());
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
            if (!_data || _data.state != this)
            {
                GameObject dataObject = new(name + "_enhancementData");
                dataObject.transform.SetParent(transform, false);
                _data = dataObject.AddComponent<LightSyncEnhancementData>();
                _data.state = this;
            }
            if (_data)
            {
                _data.gameObject.name = new(name + "_enhancementData");
                _data.RefreshHideFlags();
                OnDataObjectCreated(_data);
            }
        }
#endif
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && player.isLocal)
            {
                Networking.SetOwner(player, _data.gameObject);
            }
        }

        public virtual void OnEnable()
        {
            Networking.SetOwner(Networking.GetOwner(gameObject), _data.gameObject);
        }
    }
}
