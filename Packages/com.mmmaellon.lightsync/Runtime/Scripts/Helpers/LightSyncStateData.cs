using System.Collections.Generic;
using UnityEngine;
using UdonSharp;
namespace MMMaellon.LightSync
{
    public abstract class LightSyncStateData : UdonSharpBehaviour
    {
        public LightSyncStateWithData state;
        public override void OnDeserialization()
        {
            state.OnDataDeserialization();
        }
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public virtual void OnValidate()
        {
            RefreshHideFlags();
        }

        public virtual void RefreshHideFlags()
        {
            if (state && state.sync && state.stateData == this)
            {
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
