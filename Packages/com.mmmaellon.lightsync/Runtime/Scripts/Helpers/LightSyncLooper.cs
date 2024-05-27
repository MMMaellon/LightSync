
using System.Collections.Generic;
using UdonSharp;
using UnityEngine;

namespace MMMaellon.LightSync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public abstract class LightSyncLooper : UdonSharpBehaviour
    {
        public LightSync sync;
        public LightSyncData data;
        [System.NonSerialized]
        public float startTime = 0;
        [System.NonSerialized]
        public float elapsedTime = 0;

        public void Loop()
        {
            if (firstLerp)
            {
                firstLerp = false;
                startTime = Time.timeSinceLevelLoad;
                elapsedTime = 0;
                if (data.IsOwner())
                {
                    data.RequestSerialization();
                }
            }
            else
            {
                elapsedTime = Time.timeSinceLevelLoad - startTime;
            }
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

    }
}
