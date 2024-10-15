
using UdonSharp;
using UnityEngine;
using VRC;
using VRC.Udon;
using UdonSharpEditor;
using UdonSharp.Internal;




#if UNITY_EDITOR && !COMPILER_UDONSHARP
using System.Collections.Generic;
#endif

namespace MMMaellon.LightSync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public abstract class LightSyncLooper : UdonSharpBehaviour
    {
        public LightSync sync;
        [System.NonSerialized]
        public float startTime = 0;
        [System.NonSerialized]
        public float elapsedTime = 0;
        [System.NonSerialized]
        public float firstLoopTime = 0;

        bool shouldSync;
        public void Loop()
        {
            if (!enabled)
            {
                //prevents a race condition
                return;
            }
            if (firstLerp)
            {
                shouldSync = true;
                firstLerp = false;
                firstLoopTime = Time.timeSinceLevelLoad;
                elapsedTime = 0;
            }
            else
            {
                elapsedTime = Time.timeSinceLevelLoad - firstLoopTime;
            }
            if (!sync.OnLerp(elapsedTime, GetAutoSmoothedInterpolation(Time.realtimeSinceStartup - startTime)))
            {
                StopLoop();
            }
            if (shouldSync)
            {
                shouldSync = false;
                sync.data.RequestSync();
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

        public bool firstLerp;
        public float lerpPeriod;
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

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public void OnValidate()
        {
            RefreshHideFlags();
        }

        public void RefreshHideFlagsAsync()
        {
            StartCoroutine(RefreshHideFlagsEnum());
        }

        public IEnumerator<WaitForSeconds> RefreshHideFlagsEnum()
        {
            yield return new WaitForSeconds(0);
            RefreshHideFlags();
        }

        public virtual void RefreshHideFlags()
        {
            if (sync == null || (sync.separateHelperObjects != (sync.gameObject != gameObject)))
            {
                //don't delete because this needs to run on validation which happens when you first add the component
                sync = null;
                gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
                hideFlags &= ~HideFlags.HideInInspector;
                return;
            }
            if (sync)
            {
                if (sync.showInternalObjects)
                {
                    gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
                    hideFlags &= ~HideFlags.HideInInspector;
                }
                else if (sync.separateHelperObjects)
                {
                    hideFlags &= ~HideFlags.HideInInspector;
                    gameObject.hideFlags |= HideFlags.HideInHierarchy;
                }
                else
                {
                    hideFlags |= HideFlags.HideInInspector;
                }
            }
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
            gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
            hideFlags &= ~HideFlags.HideInInspector;
            var obj = gameObject; //gameobject becomes null after next line
            UdonSharpEditorUtility.DestroyImmediate(this);
            Singleton.DestroyEmptyGameObject(obj);
        }

#endif
    }
}
