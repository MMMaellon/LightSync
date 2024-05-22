
using UdonSharp;
using UnityEngine;

namespace MMMaellon.LightSync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LightSyncLooper : UdonSharpBehaviour
    {
        public LightSync sync;
        public LightSyncData data;
        [System.NonSerialized]
        public float startTime = 0;
        [System.NonSerialized]
        public float elapsedTime = 0;
        public void FixedUpdate()
        {
            if (firstLerp)
            {
                firstLerp = false;
                startTime = Time.timeSinceLevelLoad;
                if (data.IsOwner())
                {
                    data.RequestSerialization();
                }
            }
            elapsedTime = Time.timeSinceLevelLoad - startTime;
            if (!sync.OnLerp(elapsedTime, GetAutoSmoothedInterpolation(elapsedTime)))
            {
                StopLoop();
            }
        }
        public float GetAutoSmoothedInterpolation(float elapsedTime)
        {
            return data.autoSmoothingTime <= 0 ? 1 : elapsedTime / data.autoSmoothingTime;
        }

        bool firstLerp;
        public void StartLoop()
        {
            firstLerp = true;
            enabled = true;
        }
        public void StopLoop()
        {
            enabled = false;
        }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
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
                }
            }

            gameObject.hideFlags = HideFlags.None;
        }
#endif
    }
}
