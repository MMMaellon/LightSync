
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;

namespace MMMaellon.LightSync
{
    [RequireComponent(typeof(LightSync))]
    public abstract class LightSyncStateWithData : LightSyncState
    {
        [HideInInspector]
        public LightSyncStateData stateData;

        public abstract void OnDataObjectCreation(LightSyncStateData enhancementData);
        public abstract void OnDataDeserialization();
        public abstract string GetDataTypeName();

#if !COMPILER_UDONSHARP && UNITY_EDITOR

        public void OnDestroy()
        {
            DestroyInternalObjectsAsync();
        }

        public void DestroyInternalObjectsAsync()
        {
            if (stateData)
            {
                stateData.DestroyAsync();
                stateData = null;
            }
        }

        public override void AutoSetup()
        {
            base.AutoSetup();
            CreateDataObject();
        }

        public virtual void RefreshFlags()
        {
            if (!stateData)
            {
                AutoSetup();
            }
            else
            {
                stateData.RefreshHideFlags();
            }
        }

        public virtual void CreateDataObject()
        {
            if (!stateData || stateData.state != this)
            {
                System.Type dataType = System.Type.GetType(GetDataTypeName());
                if (dataType == null || dataType.ToString() == "")
                {
                    Debug.LogWarning($"ERROR: Invalid type name from GetDataTypeName() of {GetType().FullName}. Make sure to include the full namespace");
                    return;
                }
                if (!dataType.IsSubclassOf(typeof(LightSyncStateData)))
                {
                    Debug.LogWarning($"ERROR: {GetType().FullName} cannot be setup because {dataType.FullName} does not inherit from {typeof(LightSyncStateData).FullName}");
                    return;
                }
                GameObject dataObject = new(name + "_statedata" + stateID);
                dataObject.transform.SetParent(transform, false);
                stateData = UdonSharpComponentExtensions.AddUdonSharpComponent(dataObject, dataType).GetComponent<LightSyncStateData>();
            }
            if (stateData)
            {
                GameObject dataObject = stateData.gameObject;
                if (!PrefabUtility.IsPartOfAnyPrefab(dataObject))
                {
                    if (sync.unparentInternalObjects && dataObject.transform.parent != null)
                    {
                        dataObject.transform.SetParent(null, false);
                    }
                    else if (!sync.unparentInternalObjects && dataObject.transform.parent != transform)
                    {
                        dataObject.transform.SetParent(transform, false);
                    }
                }
                dataObject.name = name + "_statedata" + stateID;
                stateData.state = this;
                stateData.RefreshHideFlags();
                OnDataObjectCreation(stateData);
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
