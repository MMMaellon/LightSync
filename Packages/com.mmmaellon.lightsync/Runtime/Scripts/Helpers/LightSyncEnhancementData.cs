using System.Collections.Generic;
using UdonSharp;
using UnityEngine;
namespace MMMaellon.LightSync
{
    public abstract class LightSyncEnhancementData : UdonSharpBehaviour
    {
        public LightSyncEnhancementWithData enhancement;
        //IGNORE
        //These are just here to prevent Unity log spam in the Editor
        bool _showInternalObjects;
        public override void OnDeserialization()
        {
            // enhancement.OnDataDeserialization();
        }
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public virtual void OnValidate()
        {
            RefreshHideFlags();
        }

        public virtual void RefreshHideFlags()
        {
            if (enhancement != null && enhancement.sync != null)
            {
                if (enhancement.enhancementData == this)
                {
                    if (enhancement.sync.showInternalObjects == _showInternalObjects)
                    {
                        return;
                    }
                    _showInternalObjects = enhancement.sync.showInternalObjects;
                    if (enhancement.sync.showInternalObjects)
                    {
                        gameObject.hideFlags = HideFlags.None;
                    }
                    else
                    {
                        gameObject.hideFlags = HideFlags.HideInHierarchy;
                    }
                    return;
                }
                else
                {
                    enhancement = null;
                    DestroyAsync();
                }
            }

            gameObject.hideFlags = HideFlags.None;
        }

        public void DestroyAsync()
        {
            if (gameObject.activeInHierarchy && enabled)//prevents log spam in play mode
            {
                StartCoroutine(Destroy());
            }
        }

        public IEnumerator<WaitForSeconds> Destroy()
        {
            yield return new WaitForSeconds(0);
            DestroyImmediate(gameObject);
        }
#endif
    }
}
