
using UnityEngine;
using VRC.SDKBase;

namespace MMMaellon.LightSync
{
    [RequireComponent(typeof(LightSync))]
    public abstract class LightSyncEnhancementWithData : LightSyncEnhancement
    {
        [HideInInspector]
        public LightSyncEnhancementData enhancementData;

        public abstract void OnDataObjectCreation(LightSyncEnhancementData enhancementData);
        public abstract void OnDataDeserialization();
        public abstract string GetDataTypeName();

#if !COMPILER_UDONSHARP && UNITY_EDITOR

        public void OnDestroy()
        {
            if (enhancementData)
            {
                enhancementData.DestroyAsync();
            }
        }

        public override void AutoSetup()
        {
            base.AutoSetup();
            CreateDataObject();
        }

        public virtual void CreateDataObject()
        {
            if (!enhancementData || enhancementData.enhancement != this)
            {
                System.Type dataType = System.Type.GetType(GetDataTypeName());
                if (dataType == null || dataType.ToString() == "")
                {
                    Debug.LogWarning($"ERROR: Invalid type name from GetDataTypeName() of {GetType().FullName}. Make sure to include the full namespace");
                    return;
                }
                if (!dataType.IsSubclassOf(typeof(LightSyncEnhancementData)))
                {
                    Debug.LogWarning($"ERROR: {GetType().FullName} cannot be setup because {dataType.FullName} does not inherit from {typeof(LightSyncEnhancementData).FullName}");
                    return;
                }
                GameObject dataObject = new(name + "_enhancementData");
                enhancementData = dataObject.AddComponent(dataType).GetComponent<LightSyncEnhancementData>();
            }
            if (enhancementData)
            {
                GameObject dataObject = enhancementData.gameObject;
                if (sync.unparentInternalObjects && dataObject.transform.parent != null)
                {
                    dataObject.transform.SetParent(null, false);
                }
                else if (!sync.unparentInternalObjects && dataObject.transform.parent != transform)
                {
                    dataObject.transform.SetParent(transform, false);
                }
                dataObject.name = name + "_enhancementData";
                enhancementData.enhancement = this;
                enhancementData.RefreshHideFlags();
                OnDataObjectCreation(enhancementData);
            }
        }
#endif
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && player.isLocal)
            {
                Networking.SetOwner(player, enhancementData.gameObject);
            }
        }

        // public virtual void OnEnable()
        // {
        //     if (Networking.LocalPlayer.IsOwner(gameObject))
        //     {
        //         Networking.SetOwner(Networking.LocalPlayer, _data.gameObject);
        //     }
        // }
    }
}
