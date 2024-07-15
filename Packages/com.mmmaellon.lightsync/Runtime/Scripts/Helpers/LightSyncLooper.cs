
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
        [System.NonSerialized]
        public float firstLoopTime = 0;

        public void Loop()
        {
            if (!enabled)
            {
                //prevents a race condition
                return;
            }
            if (firstLerp)
            {
                firstLerp = false;
                firstLoopTime = Time.timeSinceLevelLoad;
                elapsedTime = 0;
                if (sync.IsOwner())
                {
                    data.RequestSerialization();
                }
            }
            else
            {
                elapsedTime = Time.timeSinceLevelLoad - firstLoopTime;
            }
            if (!sync.OnLerp(elapsedTime, GetAutoSmoothedInterpolation(Time.realtimeSinceStartup - startTime)))
            {
                StopLoop();
            }
        }

        public virtual float GetAutoSmoothedInterpolation(float realElapsedTime)
        {
            if (sync.teleportFlag)
            {
                sync.teleportFlag = false;
                return 1;
            }
            // return lerpPeriod <= 0 ? 1 : (Time.realtimeSinceStartup - startTime) / lerpPeriod;
            return lerpPeriod <= 0 ? 1 : realElapsedTime / lerpPeriod;
        }

        protected bool firstLerp;
        protected float lerpPeriod;
        public void StartLoop()
        {
            firstLerp = true;
            enabled = true;
            startTime = Time.realtimeSinceStartup;
            lerpPeriod = sync.autoSmoothingTime;
        }

        public void StopLoop()
        {
            enabled = false;
        }
    }
}
