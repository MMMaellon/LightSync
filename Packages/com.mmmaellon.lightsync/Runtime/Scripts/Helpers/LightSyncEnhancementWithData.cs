
using UnityEngine;
using VRC.SDKBase;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UdonSharpEditor;
using UnityEditor;
#endif

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
            DestroyInternalObjectsAsync();
        }

        public void DestroyInternalObjects()
        {
            if (enhancementData)
            {
                enhancementData.Destroy();
                enhancementData = null;
            }
        }
        public void DestroyInternalObjectsAsync()
        {
            if (enhancementData)
            {
                enhancementData.DestroyAsync();
                enhancementData = null;
            }
        }

        public override void AutoSetup()
        {
            base.AutoSetup();
            CreateDataObject();
            PrefabUtility.RecordPrefabInstancePropertyModifications(enhancementData);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }

        public virtual void RefreshFlags()
        {
            if (!enhancementData)
            {
                AutoSetup();
            }
            else
            {
                enhancementData.RefreshHideFlags();
            }
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
                GameObject dataObject = new(name + "_enhancementData_" + GUID.Generate());
                enhancementData = UdonSharpComponentExtensions.AddUdonSharpComponent(dataObject, dataType).GetComponent<LightSyncEnhancementData>();
            }
            if (enhancementData)
            {
                GameObject dataObject = enhancementData.gameObject;
                if (!PrefabUtility.IsPartOfAnyPrefab(dataObject))
                {
                    if (sync.separateHelperObjects)
                    {
                        if (dataObject.transform.parent != null)
                        {
                            dataObject.transform.SetParent(null, false);
                        }
                    }
                    else if (dataObject.transform.parent != transform)
                    {
                        dataObject.transform.SetParent(transform, false);
                    }
                }
                // dataObject.name = name + "_enhancementData";
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
        //     if (Networking.LocalPlayer.IsOwner(gameObject) && !Networking.LocalPlayer.IsOwner(enhancementData.gameObject))
        //     {
        //         Networking.SetOwner(Networking.LocalPlayer, enhancementData.gameObject);
        //     }
        // }
    }
}
