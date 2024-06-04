using System.Collections.Generic;
using UnityEngine;
using UdonSharp;
namespace MMMaellon.LightSync
{
    public abstract class LightSyncStateData : UdonSharpBehaviour
    {
        public LightSyncStateWithData state;
        //IGNORE
        //These are just here to prevent Unity log spam in the Editor
        bool _showInternalObjects;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public virtual void OnValidate()
        {
            RefreshHideFlags();
        }

        public virtual void RefreshHideFlags()
        {
            if (state != null && state.sync != null)
            {
                if (state.stateData == this)
                {
                    if (state.sync.showInternalObjects == _showInternalObjects)
                    {
                        return;
                    }
                    _showInternalObjects = state.sync.showInternalObjects;
                    if (state.sync.showInternalObjects)
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
                    state = null;
                    DestroyAsync();
                }
            }

            gameObject.hideFlags = HideFlags.None;
        }

        public void DestroyAsync()//prevents log spam in play mode
        {
            if (gameObject.activeInHierarchy && enabled)
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
