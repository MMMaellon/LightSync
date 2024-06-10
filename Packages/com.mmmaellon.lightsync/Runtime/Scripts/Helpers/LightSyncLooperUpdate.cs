using System.Collections.Generic;
using UnityEngine;

namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]//prevents it from showing up in the add component menu
    public class LightSyncLooperUpdate : LightSyncLooper
    {
        public void Update()
        {
            Loop();
        }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        //ONLY Update need this part because all the loopers are on the same object

        public void OnValidate()
        {
            RefreshHideFlags();
        }

        public void RefreshHideFlags()
        {
            if (sync != null)
            {
                if (sync.looper == this)
                {
                    if (sync.showInternalObjects)
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
                    sync = null;
                    DestroyAsync();//can't delete synchronously in OnValidate
                }
            }

            gameObject.hideFlags = HideFlags.None;
        }

        public void DestroyAsync()
        {
            if (gameObject.activeInHierarchy)//prevents log spam in play mode
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
