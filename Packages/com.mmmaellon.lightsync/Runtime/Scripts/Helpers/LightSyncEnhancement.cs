
using System.Collections.Generic;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace MMMaellon.LightSync
{
    [RequireComponent(typeof(LightSync))]
    public abstract class LightSyncEnhancement : UdonSharpBehaviour
    {
        [HideInInspector]
        public LightSync sync;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        public virtual void Reset()
        {
            AutoSetup();
        }

        public virtual void AutoSetup()
        {
            Debug.LogWarning("past wait");
            if (!sync)
            {
                Debug.LogWarning("no sync");
                sync = GetComponent<LightSync>();
            }
            Debug.LogWarning("end");
        }
#endif
    }
}
